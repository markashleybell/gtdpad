using System.Linq;
using Nancy;
using Nancy.Security;
using Nancy.Authentication.Forms;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace gtdpad
{
    public class MainModule : NancyModule
    {
        private JsonSerializerSettings _jsonSettings = new JsonSerializerSettings {
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
            Formatting = Formatting.Indented
        };

        public MainModule(IRepository db)
        {
            Get("/", args => {
                this.RequiresAuthentication();

                // Fetch the initial data for this page
                var pages = db.ReadPages(this.GetUser().Identifier);
                var page = pages.First();

                // Build up the initial data structure
                var data = new { 
                    contentData = db.ReadPageDeep(page.ID),
                    sidebarData = new {
                        pages = pages 
                    }
                };
                
                var model = new IndexViewModel {
                    InitialData = JsonConvert.SerializeObject(data, _jsonSettings)
                };

                return View["index.html", model];
            });

            Get("/{id:guid}", args => {
                this.RequiresAuthentication();
                
                var pages = db.ReadPages(this.GetUser().Identifier);

                // Build up the initial data structure
                var data = new { 
                    contentData = db.ReadPageDeep(args.id),
                    sidebarData = new {
                        pages = pages 
                    }
                };
                
                var model = new IndexViewModel {
                    InitialData = JsonConvert.SerializeObject(data, _jsonSettings)
                };

                return View["index.html", model];
            });

            Get("/login", args => {
                return View["login.html"];
            });

            Post("/login", args => {
                var id = db.ValidateUser((string)this.Request.Form.Username, (string)this.Request.Form.Password);
                return this.LoginAndRedirect(id.Value, null);
            });

            Get("/logout", args => {
                return this.LogoutAndRedirect("~/");
            });

            Get("/tests", args => {
                return View["tests/tests.html"];
            });
        }
    }
}