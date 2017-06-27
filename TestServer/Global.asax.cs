using System.Data.Entity;
using System.Web.Mvc;
using System.Web.Routing;
using TestServer.Models;

namespace TestServer
{
    public class MvcApplication : System.Web.HttpApplication
    {
        protected void Application_Start()
        {
            Database.SetInitializer(new DbInitializer());

            AreaRegistration.RegisterAllAreas();
            RouteConfig.RegisterRoutes(RouteTable.Routes);
            //var refreshListOfBestFaces = new Thread(PhotoController.RefreshListOfBestFaces);
            //refreshListOfBestFaces.Start();
        }
    }
}
