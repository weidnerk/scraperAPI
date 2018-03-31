using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using scrapeAPI.Models;
using scrapeAPI.com.ebay.developer;
using eBay.Service.Core.Soap;
using System.Web;
using Microsoft.AspNet.Identity.Owin;
using System.Threading.Tasks;

namespace scrapeAPI.Controllers
{

    public class ScraperController : ApiController
    {
        DataModelsDB db = new DataModelsDB();
        const string _filename = "order.csv";
        const string _logfile = "scrape_log.txt";
        private ApplicationUserManager _userManager;
        public ApplicationUserManager UserManager
        {
            get => _userManager ?? HttpContext.Current.GetOwinContext().GetUserManager<ApplicationUserManager>();
            private set
            {
                _userManager = value;
            }
        }

        [Route("numitemssold")]
        [HttpGet]
        public async Task<IHttpActionResult> GetNumItemsSold(string seller, int daysBack, int resultsPerPg, int minSold, string showNoOrders, string userName)
        {
            try
            {
                // stub to delete a user
                //AccountController.DeleteUsr("ventures2021@gmail.com");


                string header = string.Format("Seller: {0} daysBack: {1} resultsPerPg: {2}", seller, daysBack, resultsPerPg);
                var user = await UserManager.FindByNameAsync(userName);

                var sh = new SearchHistory();
                sh.UserId = user.Id;
                sh.Seller = seller;
                sh.DaysBack = daysBack;
                sh.MinSoldFilter = minSold;
                await db.SearchHistorySave(sh);

                var profile = db.UserProfiles.Find(user.Id);
                var r = ebayAPIs.FindCompletedItems(seller, daysBack, profile.AppID);

                if (r != null)
                    return Ok(r.Count());
                else return Ok(0);
            }
            catch (Exception exc)
            {
                return Content(HttpStatusCode.ExpectationFailed, DateTime.Now.ToString() + " GetNumItemsSold " + exc.Message);
            }
        }

        [Route("getsellersold")]
        [HttpGet]
        public async Task<IHttpActionResult> FetchSeller(string seller, int daysBack, int resultsPerPg, int rptNumber, int minSold, string showNoOrders, string userName)
        {

            string baseDir = System.AppDomain.CurrentDomain.BaseDirectory;
            string log = baseDir + _logfile;
            string header = string.Format("Seller: {0} daysBack: {1} resultsPerPg: {2}", seller, daysBack, resultsPerPg);
            HomeController.WriteFile(log, header);

            var user = await UserManager.FindByNameAsync(userName);

            // test
            ebayAPIs.GetAPIStatus(user);

            var mv = GetSellerSoldAsync(seller, daysBack, resultsPerPg, rptNumber, minSold, showNoOrders, user);
            return Ok(mv);
        }

        protected ModelView GetSellerSoldAsync(string seller, int daysBack, int resultsPerPg, int rptNumber, int minSold, string showNoOrders, ApplicationUser user)
        {
            int notSold = 0;

            HttpResponseMessage message = Request.CreateResponse<ModelView>(HttpStatusCode.NoContent, null);
            var profile = db.UserProfiles.Find(user.Id);
            var completedItems = ebayAPIs.FindCompletedItems(seller, daysBack, profile.AppID);
            var listings = new List<Listing>();
            if (completedItems != null)
            {
                foreach (SearchItem searchItem in completedItems)
                {
                    //var a = searchItem.itemId;
                    //var b = searchItem.title;
                    //var c = searchItem.listingInfo.listingType;
                    //var d = searchItem.viewItemURL;
                    //var e = searchItem.sellingStatus.currentPrice.Value;
                    //var f = searchItem.galleryURL;

                    var listing = new Listing();
                    listing.Title = searchItem.title;

                    // loop through each order
                    DateTime ModTimeTo = DateTime.Now.ToUniversalTime();
                    DateTime ModTimeFrom = ModTimeTo.AddDays(-daysBack);
                    var transactions = ebayAPIs.GetItemTransactions(searchItem.itemId, ModTimeFrom, ModTimeTo, user);
                    var orderHistory = new List<OrderHistory>();
                    foreach (TransactionType item in transactions)
                    {
                        if (item.MonetaryDetails != null)
                        {
                            // why is Payment an array?
                            var pmtTime = item.MonetaryDetails.Payments.Payment[0].PaymentTime;
                            var pmtAmt = item.MonetaryDetails.Payments.Payment[0].PaymentAmount.Value;
                            var order = new OrderHistory();
                            order.Title = searchItem.title;
                            order.Qty = item.QuantityPurchased.ToString();

                            order.Price = item.TransactionPrice.Value.ToString();

                            order.DateOfPurchase = item.CreatedDate;
                            order.Url = searchItem.viewItemURL;
                            order.ImageUrl = searchItem.galleryURL;

                            orderHistory.Add(order);
                        }
                        else ++notSold;
                    }
                    db.OrderHistorySave(orderHistory, rptNumber, false);
                    listing.Orders = orderHistory;
                    listings.Add(listing);
                }
            }
            var mv = new ModelView();
            mv.Listings = listings;

            int b = notSold;
            return mv;
        }

        [Route("timessold/{rptNumber}/{minSold}/{showNoOrders}/{daysBack}")]
        [HttpGet]
        public IHttpActionResult GetTimesSold(int rptNumber, int minSold, string showNoOrders, int daysBack)
        {
            bool endedListings = (showNoOrders == "0") ? false : true;
            DateTime ModTimeTo = DateTime.Now.ToUniversalTime();
            DateTime ModTimeFrom = ModTimeTo.AddDays(-daysBack);

            try
            {
                // materialize the date from SQL Server first since SQL Server doesn't understand Convert.ToInt32 on Qty
                var results = (from c in db.OrderHistory
                               where c.RptNumber == rptNumber && c.DateOfPurchase >= ModTimeFrom
                               group c by new { c.Title, c.Url, c.Price, c.ImageUrl, c.Qty, c.DateOfPurchase } into grp
                               select new
                               {
                                   grp.Key.Title,
                                   grp.Key.Url,
                                   grp.Key.Price,
                                   grp.Key.ImageUrl,
                                   grp.Key.Qty,
                                   grp.Key.DateOfPurchase
                               }).ToList();

                var x = from a in results
                        group a by new { a.Title, a.Url, a.Price, a.ImageUrl } into grp
                        select new
                              {
                                grp.Key.Title,
                                grp.Key.Url,
                                grp.Key.Price,
                                grp.Key.ImageUrl,
                                Qty = grp.Sum(s => Convert.ToInt32(s.Qty)),
                                MaxDate = grp.Max(s => Convert.ToDateTime(s.DateOfPurchase))
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
                                  EarliestSold = g.MaxDate
                              };

                // 1. where rptNumber=x and DateOfPurchase>daysBack
                // 2. group by title,url,price,sum(Qty),max(DateofPurchase)
                // 3. having Qty>=minSold
                //var results = from c in db.OrderHistory
                //        where c.RptNumber == rptNumber && c.DateOfPurchase >= ModTimeFrom
                //        group c by new { c.Title, c.Url, c.Price, c.ImageUrl } into grp
                //        select new { grp.Key.Title,
                //                        grp.Key.Url,
                //                        grp.Key.Price,
                //                        grp.Key.ImageUrl,
                //                        Qty = grp.Sum(c => Convert.ToInt32(c.Qty)),
                //                        MaxDate = grp.Max(c => Convert.ToDateTime(c.DateOfPurchase))
                //                    } into g
                //        where g.Qty >= minSold
                //        select new TimesSold {
                //            Title = g.Title,
                //            Url = g.Url,
                //            ImageUrl = g.ImageUrl,
                //            Price = g.Price,
                //            SoldQty = g.Qty,
                //            EarliestSold = g.MaxDate
                //        };

                //var results = from c in db.OrderHistory
                //              where c.RptNumber == rptNumber && (!c.ListingEnded || c.ListingEnded == endedListings) && c.DateOfPurchase >= ModTimeFrom
                //              group c by new { c.Title, c.Url, c.RptNumber, c.ImageUrl, c.Price } into grp
                //              where grp.Count() >= minSold
                //              orderby grp.Max(x => x.DateOfPurchase) descending
                //              select new TimesSold { Title = grp.Key.Title, Url = grp.Key.Url, ImageUrl = grp.Key.ImageUrl, Price = grp.Key.Price, SoldQty = grp.Count(), EarliestSold = grp.Max(x => x.DateOfPurchase) };


                // count listings processed so far
                var listings = from o in db.OrderHistory
                               where o.RptNumber == rptNumber
                               group o by new { o.Title } into grp
                               select grp;

                // count listings processed so far - matches
                var matchedlistings = from c in results
                                      join o in db.OrderHistory on c.Title equals o.Title
                                      where o.RptNumber == rptNumber && !o.ListingEnded
                                      group c by new { c.Title, c.Url, o.RptNumber, c.ImageUrl, c.Price } into grp
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
                return Content(HttpStatusCode.ExpectationFailed, DateTime.Now.ToString() + " GetTimesSold " + exc.Message);
            }
        }

        [HttpGet]
        [Route("emailtaken")]
        public async Task<IHttpActionResult> GetEmailTaken(string email)
        {
            var user = await UserManager.FindByNameAsync(email);
            if (user == null)
                return Ok(false);
            else
                return Ok(true);
        }

        [HttpGet]
        [Route("tradingapiusage")]
        public async Task<IHttpActionResult> GetTradingAPIUsage(string userName)
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

        [HttpGet]
        [Route("tokenstatustype")]
        public async Task<IHttpActionResult> GetTokenStatus(string userName)
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
    }
}