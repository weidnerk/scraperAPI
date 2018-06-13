/*
 * Note there are 2 Web references: com.ebay.developer and com.ebay.developer1 - they are not duplicate references:
 * 
 *      com.ebay.developer is a reference to the Finding API
 *      com.ebay.developer1 is a reference to the Shopping API
 * 
 * This is notated further in the 'eBay API Website' doc
 * 
 * Uses the Trading API via the .NET SDK.  Evidenced by eBay.Service in \References
 */

using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using scrapeAPI.Models;
using System.Web;
using Microsoft.AspNet.Identity.Owin;
using System.Threading.Tasks;

namespace scrapeAPI.Controllers
{
    [Authorize]
    public class ScraperController : ApiController
    {
        DataModelsDB db = new DataModelsDB();
        const string _filename = "order.csv";
        readonly string _logfile = "scrape_log.txt";

        private ApplicationUserManager _userManager;
        public ApplicationUserManager UserManager
        {
            get => _userManager ?? HttpContext.Current.GetOwinContext().GetUserManager<ApplicationUserManager>();
            private set
            {
                _userManager = value;
            }
        }

        // Unused after developing GetSingleItem()
        [Route("prodbyid")]
        [HttpGet]
        public async Task<IHttpActionResult> GetProdById(string userName)
        {
            try
            {
                var user = await UserManager.FindByNameAsync(userName);

                var response = ebayAPIs.FindByKeyword(user);
                return Ok(response);
            }
            catch (Exception exc)
            {
                string msg = " GetProdById " + exc.Message;
                HomeController.WriteFile(_logfile, msg);
                return BadRequest(msg);
            }
        }

        [Route("numitemssold")]
        [HttpGet]
        public async Task<IHttpActionResult> GetNumItemsSold(string seller, int daysBack, int resultsPerPg, int minSold, string userName, int rptNumber)
        {
            try
            {
                // stub to delete a user
                //AccountController.DeleteUsr("ventures2021@gmail.com");
                //AccountController.DeleteUsr("aaronmweidner@gmail.com");

                var user = await UserManager.FindByNameAsync(userName);

                int itemCount = ebayAPIs.ItemCount(seller, daysBack, user, rptNumber);
                return Ok(itemCount);
            }
            catch (Exception exc)
            {
                string msg = " GetNumItemsSold " + exc.Message;
                HomeController.WriteFile(_logfile, msg);
                return BadRequest(msg);
            }
        }

        [Route("getsellersold")]
        [HttpGet]
        public async Task<IHttpActionResult> FetchSeller(string seller, int daysBack, int resultsPerPg, int rptNumber, int minSold, string userName)
        {
            try
            {
                string header = string.Format("Seller: {0} daysBack: {1} resultsPerPg: {2}", seller, daysBack, resultsPerPg);
                HomeController.WriteFile(_logfile, header);

                var user = await UserManager.FindByNameAsync(userName);

                ebayAPIs.GetAPIStatus(user);
                var sh = new SearchHistory();
                sh.UserId = user.Id;
                sh.ReportNumber = rptNumber;
                sh.Seller = seller;
                sh.DaysBack = daysBack;
                sh.MinSoldFilter = minSold;
                await db.SearchHistorySave(sh);

                var mv = await GetSellerSoldAsync(seller, daysBack, resultsPerPg, rptNumber, minSold, user);
                return Ok(mv);
            }
            catch (Exception exc)
            {
                string msg = " FetchSeller " + exc.Message;
                HomeController.WriteFile(_logfile, msg);
                return BadRequest(msg);
            }
        }

        // Recall FindCompletedItems will only give us up to 100 listings
        // interesting case is 'fabulousfinds101' - 
        protected async Task<ModelView> GetSellerSoldAsync(string seller, int daysBack, int resultsPerPg, int rptNumber, int minSold, ApplicationUser user)
        {
            HttpResponseMessage message = Request.CreateResponse<ModelView>(HttpStatusCode.NoContent, null);
            var profile = db.UserProfiles.Find(user.Id);
            return await ebayAPIs.ToStart(seller, daysBack, user, rptNumber);
        }

        [Route("getreport/{rptNumber}/{minSold}/{daysBack}/{minPrice}/{maxPrice}")]
        [HttpGet]
        public IHttpActionResult GetReport(int rptNumber, int minSold, int daysBack, int? minPrice, int? maxPrice)
        {
            //bool endedListings = (showNoOrders == "0") ? false : true;
            DateTime ModTimeTo = DateTime.Now.ToUniversalTime();
            DateTime ModTimeFrom = ModTimeTo.AddDays(-daysBack);

            try
            {
                // materialize the date from SQL Server first since SQL Server doesn't understand Convert.ToInt32 on Qty
                //var results = (from c in db.OrderHistory
                //               where c.RptNumber == rptNumber && c.DateOfPurchase >= ModTimeFrom
                //               group c by new { c.Title, c.Url, c.Price, c.ImageUrl, c.Qty, c.DateOfPurchase } into grp
                //               select new
                //               {
                //                   grp.Key.Title,
                //                   grp.Key.Url,
                //                   grp.Key.Price,
                //                   grp.Key.ImageUrl,
                //                   grp.Key.Qty,
                //                   grp.Key.DateOfPurchase
                //               }).ToList();

                var results = (from c in db.OrderHistory
                               where c.RptNumber == rptNumber && c.DateOfPurchase >= ModTimeFrom
                               select c
                               ).ToList();

                // group by title and price and filter by minSold
                var x = from a in results
                        group a by new { a.Title, a.Price } into grp
                        select new
                              {
                                grp.Key.Title,
                                Price = Convert.ToDecimal(grp.Key.Price),
                                Qty = grp.Sum(s => Convert.ToInt32(s.Qty)),
                                MaxDate = grp.Max(s => Convert.ToDateTime(s.DateOfPurchase)),
                                Url = grp.Max(s => s.Url),
                                ImageUrl = grp.Max(s => s.ImageUrl),
                                ItemId = grp.Max(s => s.ItemId)
                        } into g
                              where g.Qty >= minSold
                              orderby g.MaxDate descending
                        select new TimesSold
                              {
                                  Title = g.Title,
                                  Url = g.Url,
                                  ImageUrl = g.ImageUrl,
                                  Price = g.Price,
                                  SoldQty = g.Qty,
                                  EarliestSold = g.MaxDate,
                                  ItemId = g.ItemId
                              };

                // filter by min and max price
                if (minPrice.HasValue)
                    x = x.Where(u => u.Price >= minPrice);

                if (maxPrice.HasValue)
                    x = x.Where(u => u.Price <= maxPrice);

                // count listings processed so far
                var listings = from o in db.OrderHistory
                               where o.RptNumber == rptNumber
                               group o by new { o.Title } into grp
                               select grp;

                // count listings processed so far - matches
                var matchedlistings = from c in results
                                      join o in db.OrderHistory on c.Title equals o.Title
                                      where o.RptNumber == rptNumber && !o.ListingEnded
                                      group c by new { c.Title, c.Url, o.RptNumber, c.ImageUrl } into grp
                                      select grp;

                // count orders processed so far - matches
                var orders = from c in results
                             join o in db.OrderHistory on c.Title equals o.Title
                             where o.RptNumber == rptNumber && !o.ListingEnded
                             select o;

                var mv = new ModelViewTimesSold();
                mv.TimesSoldRpt = x.ToList();
                mv.ListingsProcessed = listings.Count();
                mv.TotalOrders = orders.Count();
                mv.MatchedListings = matchedlistings.Count();

                return Ok(mv);
            }
            catch (Exception exc)
            {
                string msg = " GetReport " + exc.Message;
                if (exc.InnerException != null)
                {
                    msg += " " + exc.InnerException.Message;
                    if (exc.InnerException.InnerException != null)
                    {
                        msg += " " + exc.InnerException.InnerException.Message;
                    }
                }
                HomeController.WriteFile(_logfile, msg);
                return BadRequest(msg);
            }
        }

        [HttpGet]
        [Route("emailtaken")]
        public async Task<IHttpActionResult> GetEmailTaken(string email)
        {
            try
            {
                // does FindByNameAsync() work with either username or email?
                var user = await UserManager.FindByNameAsync(email);
                if (user == null)
                    return Ok(false);
                else
                    return Ok(true);
            }
            catch (Exception exc)
            {
                string msg = "GetEmailTaken: " + exc.Message;
                HomeController.WriteFile(_logfile, msg);
                return BadRequest(msg);
            }
        }

        [HttpGet]
        [Route("usernametaken")]
        public async Task<IHttpActionResult> GetUsernameTaken(string username)
        {
            try
            {
                // does FindByNameAsync() work with either username or email?
                var user = await UserManager.FindByNameAsync(username);
                if (user == null)
                    return Ok(false);
                else
                    return Ok(true);
            }
            catch (Exception exc)
            {
                string msg = "GetUsernameTaken: " + exc.Message;
                HomeController.WriteFile(_logfile, msg);
                return BadRequest(msg);
            }
        }

        [HttpGet]
        [Route("tradingapiusage")]
        public async Task<IHttpActionResult> GetTradingAPIUsage(string userName)
        {
            try
            {
                var user = await UserManager.FindByNameAsync(userName);
                if (user == null)
                    return Ok(false);
                else
                {
                    var i = ebayAPIs.GetTradingAPIUsage(user);
                    return Ok(i);
                }
            }
            catch (Exception exc)
            {
                string msg = "GetTradingAPIUsage: " + exc.Message;
                HomeController.WriteFile(_logfile, msg);
                return BadRequest(msg);
            }
        }

        [HttpGet]
        [Route("tokenstatustype")]
        public async Task<IHttpActionResult> GetTokenStatus(string userName)
        {
            try
            {
                var user = await UserManager.FindByNameAsync(userName);
                if (user == null)
                    return Ok(false);
                else
                {
                    var i = ebayAPIs.GetTokenStatus(user);
                    return Ok(i);
                }
            }
            catch (Exception exc)
            {
                string msg = "GetTokenStatus: " + exc.Message;
                HomeController.WriteFile(_logfile, msg);
                return BadRequest(msg);
            }
        }

        [HttpGet]
        [Route("getsingleitem")]
        public async Task<IHttpActionResult> GetSingleItem(string userName, string itemId)
        {
            try
            {
                var user = await UserManager.FindByNameAsync(userName);
                if (user == null)
                    return Ok(false);
                else
                {
                    var i = await ebayAPIs.GetSingleItem(itemId, user);
                    return Ok(i);
                }
            }
            catch (Exception exc)
            {
                string msg = "GetTokenStatus: " + exc.Message;
                HomeController.WriteFile(_logfile, msg);
                return BadRequest(msg);
            }
        }

        [HttpPost]
        [Route("storelisting")]
        public async Task<IHttpActionResult> StoreListing(Listing listing)
        {
            try
            {
                await db.ListingSave(listing);
                return Ok();
            }
            catch (Exception exc)
            {
                string msg = HomeController.ErrMsg("StoreListing", exc);
                HomeController.WriteFile(_logfile, msg);
                return BadRequest(msg);
            }
        }
    }
}