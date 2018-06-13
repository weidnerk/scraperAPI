using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
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

        public static void WriteFile(string filename, string msg)
        {
            using (System.IO.StreamWriter file = new System.IO.StreamWriter(System.AppDomain.CurrentDomain.BaseDirectory + filename, true))
            {
                string dtStr = DateTime.Now.ToString();
                file.WriteLine(dtStr + " " + msg);
            }
        }

        public static string ErrMsg(string header, Exception exc)
        {
            string msg = header + " " + exc.Message;
            if (exc.InnerException != null)
            {
                msg += " " + exc.InnerException.Message;
                if (exc.InnerException.InnerException != null)
                {
                    msg += " " + exc.InnerException.InnerException.Message;
                }
            }
            return msg;
        }
    }
}
