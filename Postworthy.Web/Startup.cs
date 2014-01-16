using Microsoft.Owin;
using Owin;

[assembly: OwinStartupAttribute(typeof(Postworthy.Web.Startup))]
namespace Postworthy.Web
{
    public partial class Startup
    {
        public void Configuration(IAppBuilder app)
        {
            ConfigureAuth(app);
            app.MapSignalR();
        }
    }
}
