using Nancy;
// using Nancy.Security;
using Nancy.ModelBinding;
// using Nancy.Authentication.Forms;

namespace gtdpad
{
    public class ListsModule : NancyModule
    {
        public ListsModule(IRepository db) : base("/pages/{pageid:guid}/lists")
        {
            Post("/", args => {
                return db.CreateList(this.Bind<List>().SetDefaults<List>());
            });

            Get("/{id:guid}", args => {
                return db.ReadList(args.id);
            });

            Put("/{id:guid}", args => {
                return db.UpdateList(this.Bind<List>().SetDefaults<List>());
            });

            Delete("/{id:guid}", args => {
                return db.DeleteList(args.id);
            });

            Get("/", args => {
                return db.ReadLists(args.pageid);
            });
        }
    }
}