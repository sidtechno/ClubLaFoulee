using Microsoft.Owin;
using Owin;

[assembly: OwinStartupAttribute(typeof(ClubLaFoulee.Startup))]
namespace ClubLaFoulee
{
    public partial class Startup
    {
        public void Configuration(IAppBuilder app)
        {
            ConfigureAuth(app);
        }
    }
}
