using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Security.Claims;
using Dapper;
using Microsoft.AspNetCore.Identity;
using Nancy;
using Nancy.Authentication.Forms;

namespace gtdpad
{
    public class Repository : IRepository, IUserMapper
    {
        private readonly string _connectionString;
        private readonly PasswordHasher<User> _pwd;

        public Repository(string connectionString)
        {
            _connectionString = connectionString;
            _pwd = new PasswordHasher<User>();

            DefaultTypeMap.MatchNamesWithUnderscores = true;
        }

        /* BEGIN Forms Auth methods */

        public ClaimsPrincipal GetUserFromIdentifier(Guid identifier, NancyContext context)
        {
            var userRecord = GetSingle<User>("SELECT * FROM users WHERE id = @p0", identifier);
            var identity = new GTDPadIdentity(userRecord.ID, userRecord.Username);

            return userRecord == null ? null : new ClaimsPrincipal(identity);
        }

        public Guid? ValidateUser(string username, string password)
        {
            var userRecord = GetSingle<User>("SELECT * FROM users WHERE username = @p0", username);

            if (userRecord == null)
            {
                return null;
            }

            var hashResult = _pwd.VerifyHashedPassword(null, userRecord.Password, password);

            if (hashResult != PasswordVerificationResult.Success)
            {
                return null;
            }

            return userRecord.ID;
        }

        public Guid? GetUserID(string username)
        {
            var userRecord = GetSingle<User>("SELECT * FROM users WHERE username = @p0", username);

            if (userRecord == null)
            {
                return null;
            }

            return userRecord.ID;
        }

        public Guid CreateUser(string username, string password)
        {
            var id = Guid.NewGuid();
            var hash = _pwd.HashPassword(null, password);

            Execute("INSERT INTO users (id, username, password) VALUES (@p0, @p1, @p2)", id, username, hash);

            return id;
        }

        /* END Forms Auth methods */

        /* BEGIN Page methods */

        public Page CreatePage(Page page)
        {
            Execute("INSERT INTO pages (id, user_id, title) VALUES (@p0, @p1, @p2)", page.ID, page.UserID, page.Title);
            return ReadPage(page.ID);
        }

        public Page ReadPage(Guid id) =>
            GetSingle<Page>("SELECT * FROM pages WHERE id = @p0 AND deleted is null", id);

        public Page ReadPageDeep(Guid id)
        {
            using var conn = new SqlConnection(_connectionString);
            using var multi = conn.QueryMultiple("ReadPageDeep", new { id }, commandType: CommandType.StoredProcedure);

            var page = multi.Read<Page>().Single();
            var lists = multi.Read<List>().ToList();
            var items = multi.Read<Item>().ToList();

            lists.ForEach(list => list.Items = items.Where(item => item.ListID == list.ID));
            page.Lists = lists;

            return page;
        }

        public Page UpdatePage(Page page)
        {
            Execute("UPDATE pages SET title = @p1 WHERE id = @p0", page.ID, page.Title);
            return ReadPage(page.ID);
        }

        public Page DeletePage(Guid id) =>
            GetSingle<Page>("UPDATE pages SET deleted = @p1 WHERE id = @p0; SELECT * FROM pages WHERE id = @p0;", id, DateTime.Now);

        public IEnumerable<Page> ReadPages(Guid userID) =>
            GetMultiple<Page>("SELECT * FROM pages WHERE user_id = @p0 AND deleted is null ORDER BY display_order, created", userID);

        public Guid ReadDefaultPageID(Guid userID)
        {
            var page = GetSingle<Page>("SELECT TOP 1 * FROM pages WHERE user_id = @p0 AND deleted is null ORDER BY display_order, created", userID);
            return page == null ? Guid.Empty : page.ID;
        }

        public void UpdatePageDisplayOrder(Ordering ordering) =>
            ExecuteProc("UpdatePageDisplayOrder", new { userID = ordering.ID, order = ordering.Order });

        /* END Page methods */

        /* BEGIN List methods */

        public List CreateList(List list)
        {
            Execute("INSERT INTO lists (id, page_id, title) VALUES (@p0, @p1, @p2)", list.ID, list.PageID, list.Title);
            return ReadList(list.ID);
        }

        public List ReadList(Guid id) =>
            GetSingle<List>("SELECT * FROM lists WHERE id = @p0 AND deleted is null", id);

        public List UpdateList(List list)
        {
            Execute("UPDATE lists SET page_id = @p1, title = @p2 WHERE id = @p0", list.ID, list.PageID, list.Title);
            return ReadList(list.ID);
        }

        public List DeleteList(Guid id) =>
            GetSingle<List>("UPDATE lists SET deleted = @p1 WHERE id = @p0; SELECT * FROM lists WHERE id = @p0;", id, DateTime.Now);

        public IEnumerable<List> ReadLists(Guid pageID) =>
            GetMultiple<List>("SELECT * FROM lists WHERE page_id = @p0 AND deleted is null ORDER BY display_order, created", pageID);

        public void UpdateListDisplayOrder(Ordering ordering) =>
            ExecuteProc("UpdateListDisplayOrder", new { pageID = ordering.ID, order = ordering.Order });

        public void MoveListToTopOfPage(Guid id) => Execute("UPDATE lists SET display_order = -1 WHERE id = @p0;", id);

        /* END List methods */

        /* BEGIN Item methods */

        public Item CreateItem(Item item)
        {
            Execute("INSERT INTO items (id, list_id, title, body) VALUES (@p0, @p1, @p2, @p3)", item.ID, item.ListID, item.Title, item.Body);
            return ReadItem(item.ID);
        }

        public Item ReadItem(Guid id) =>
            GetSingle<Item>("SELECT * FROM items WHERE id = @p0 AND deleted is null", id);

        public Item UpdateItem(Item item)
        {
            Execute("UPDATE items SET list_id = @p1, title = @p2, body = @p3 WHERE id = @p0", item.ID, item.ListID, item.Title, item.Body);
            return ReadItem(item.ID);
        }

        public Item DeleteItem(Guid id) =>
            GetSingle<Item>("UPDATE items SET deleted = @p1 WHERE id = @p0; SELECT * FROM items WHERE id = @p0;", id, DateTime.Now);

        public IEnumerable<Item> ReadItems(Guid listID) =>
            GetMultiple<Item>("SELECT * FROM items WHERE list_id = @p0 AND deleted is null ORDER BY display_order, created", listID);

        public void UpdateItemDisplayOrder(Ordering ordering) =>
            ExecuteProc("UpdateItemDisplayOrder", new { listID = ordering.ID, order = ordering.Order });

        /* END Item methods */

        private DynamicParameters ConvertParameters(object[] parameters)
        {
            if (parameters == null || parameters.Length == 0)
            {
                return null;
            }

            var paramList = new Dictionary<string, object>();

            for (var i = 0; i < parameters.Length; i++)
            {
                if (parameters[i] == DBNull.Value)
                {
                    paramList.Add("@p" + i, null);
                }
                else
                {
                    paramList.Add("@p" + i, parameters[i]);
                }
            }

            return new DynamicParameters(paramList);
        }

        private T GetSingle<T>(string sql, params object[] parameters)
        {
            using var conn = new SqlConnection(_connectionString);
            return conn.Query<T>(sql, ConvertParameters(parameters)).FirstOrDefault();
        }

        private IEnumerable<T> GetMultiple<T>(string sql, params object[] parameters)
        {
            using var conn = new SqlConnection(_connectionString);
            return conn.Query<T>(sql, ConvertParameters(parameters)).ToList();
        }

        private void Execute(string sql, params object[] parameters)
        {
            using var conn = new SqlConnection(_connectionString);
            conn.Execute(sql, ConvertParameters(parameters));
        }

        private void ExecuteProc(string sql, object parameters)
        {
            using var conn = new SqlConnection(_connectionString);
            conn.Execute(sql, parameters, commandType: CommandType.StoredProcedure);
        }
    }
}
