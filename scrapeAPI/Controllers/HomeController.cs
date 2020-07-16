/*
 * 
 * Don't remove - gets us to the Web API home page.
 * 
 * 
 */

using System.Web.Mvc;

namespace scrapeAPI.Controllers
{
    public class HomeController : Controller
    {
        public ActionResult Index()
        {
            ViewBag.Title = "Home Page";

            return View();
        }

    }
}