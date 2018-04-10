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

        [Route("numitemssold")]
        [HttpGet]
        public async Task<IHttpActionResult> GetNumItemsSold(string seller, int daysBack, int resultsPerPg, int minSold, string showNoOrders, string userName, int rptNumber)
        {
            try
            {
                // stub to delete a user
                //AccountController.DeleteUsr("ventures2021@gmail.com");
                //AccountController.DeleteUsr("aaronmweidner@gmail.com");

                string header = string.Format("Seller: {0} daysBack: {1} resultsPerPg: {2}", seller, daysBack, resultsPerPg);
                var user = await UserManager.FindByNameAsync(userName);

                var sh = new SearchHistory();
                sh.UserId = user.Id;
                sh.ReportNumber = rptNumber;
                sh.Seller = seller;
                sh.DaysBack = daysBack;
                sh.MinSoldFilter = minSold;
                await db.SearchHistorySave(sh);

                var profile = db.UserProfiles.Find(user.Id);
                var r = ebayAPIs.FindCompletedItems(seller, daysBack, profile.AppID, 1);

                if (r != null)
                    return Ok(r.item.Count());
                else return Ok(0);
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
        public async Task<IHttpActionResult> FetchSeller(string seller, int daysBack, int resultsPerPg, int rptNumber, int minSold, string showNoOrders, string userName)
        {
            try
            {
                string header = string.Format("Seller: {0} daysBack: {1} resultsPerPg: {2}", seller, daysBack, resultsPerPg);
                HomeController.WriteFile(_logfile, header);

                var user = await UserManager.FindByNameAsync(userName);

                // test
                ebayAPIs.GetAPIStatus(user);

                var mv = GetSellerSoldAsync(seller, daysBack, resultsPerPg, rptNumber, minSold, showNoOrders, user);
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
        protected ModelView GetSellerSoldAsync(string seller, int daysBack, int resultsPerPg, int rptNumber, int minSold, string showNoOrders, ApplicationUser user)
        {
            int notSold = 0;

            HttpResponseMessage message = Request.CreateResponse<ModelView>(HttpStatusCode.NoContent, null);
            var profile = db.UserProfiles.Find(user.Id);

            
            var completedItems = ebayAPIs.FindCompletedItems(seller, daysBack, profile.AppID, 1);
            List<SearchItemCustom> completedItemsList = completedItems.item.ToList().ConvertAll(x => new SearchItemCustom
            {
                PageNumber = 1,
                searchItem = x
            });

            #region PAGE_2
            // Ran across seller 'fabulousfinds101' which seems to be halting and not giving back all results.
            // but in that case, many of the items where ended listings so just appeared that way.
            // Then i found out about asking for another 'page' and it appeared I got more sold listings.
            // However, I ran page 2 for justforyou and looks like just got a duplicate of page 1.
            // For now, don't worry about it and just stick with page 1 until more understood.

            //if (completedItems.item.Count() >= 100)
            //{
            //    var listToAdd = ebayAPIs.FindCompletedItems(seller, daysBack, profile.AppID, 2);
            //    List<SearchItemCustom> listToAddList = completedItems.item.ToList().ConvertAll(x => new SearchItemCustom
            //    {
            //        PageNumber = 2,
            //        searchItem = x
            //    });
            //    completedItemsList.AddRange(listToAddList);

            //    if (listToAdd.item.ToList().Count() >= 100)
            //    {
            //        var anotherlistToAdd = ebayAPIs.FindCompletedItems(seller, daysBack, profile.AppID, 3);
            //        List<SearchItemCustom> anotherlistToAddList = completedItems.item.ToList().ConvertAll(x => new SearchItemCustom
            //        {
            //            PageNumber = 3,
            //            searchItem = x
            //        });
            //        completedItemsList.AddRange(anotherlistToAddList);

            //        if (anotherlistToAdd.item.ToList().Count() >= 100)
            //        {
            //            var list2 = ebayAPIs.FindCompletedItems(seller, daysBack, profile.AppID, 3);
            //            List<SearchItemCustom> list2List = completedItems.item.ToList().ConvertAll(x => new SearchItemCustom
            //            {
            //                PageNumber = 4,
            //                searchItem = x
            //            });
            //            completedItemsList.AddRange(list2List);
            //        }
            //    }
            //}
            #endregion

            var listings = new List<Listing>();
            if (completedItems != null)
            {
                foreach (SearchItemCustom searchItem in completedItemsList)
                {
                    //var a = searchItem.itemId;
                    //var b = searchItem.title;
                    //var c = searchItem.listingInfo.listingType;
                    //var d = searchItem.viewItemURL;
                    //var e = searchItem.sellingStatus.currentPrice.Value;
                    //var f = searchItem.galleryURL;

                    var listing = new Listing();
                    listing.Title = searchItem.searchItem.title;

                    // loop through each order
                    DateTime ModTimeTo = DateTime.Now.ToUniversalTime();
                    DateTime ModTimeFrom = ModTimeTo.AddDays(-daysBack);
                    TransactionTypeCollection transactions = null;
                    try
                    {
                        transactions = ebayAPIs.GetItemTransactions(searchItem.searchItem.itemId, ModTimeFrom, ModTimeTo, user);
                        var orderHistory = new List<OrderHistory>();
                        foreach (TransactionType item in transactions)
                        {
                            if (item.MonetaryDetails != null)
                            {
                                var pmtTime = item.MonetaryDetails.Payments.Payment[0].PaymentTime;
                                var pmtAmt = item.MonetaryDetails.Payments.Payment[0].PaymentAmount.Value;
                                var order = new OrderHistory();
                                order.Title = searchItem.searchItem.title;
                                order.Qty = item.QuantityPurchased.ToString();

                                order.Price = item.TransactionPrice.Value.ToString();

                                order.DateOfPurchase = item.CreatedDate;
                                order.Url = searchItem.searchItem.viewItemURL;
                                order.ImageUrl = searchItem.searchItem.galleryURL;
                                order.PageNumber = searchItem.PageNumber;

                                orderHistory.Add(order);
                            }
                        }
                        if (transactions.Count == 0)
                            ++notSold;
                        else
                        {
                            db.OrderHistorySave(orderHistory, rptNumber, false);
                            listing.Orders = orderHistory;
                            listings.Add(listing);
                        }
                    }
                    catch (Exception exc)
                    {
                        string msg = " GetItemTransactions " + exc.Message;
                        HomeController.WriteFile(_logfile, msg);
                    }
                }
            }
            var mv = new ModelView();
            mv.Listings = listings;

            int b = notSold;
            return mv;
        }

        [Route("timessold/{rptNumber}/{minSold}/{showNoOrders}/{daysBack}/{minPrice}/{maxPrice}")]
        [HttpGet]
        public IHttpActionResult GetTimesSold(int rptNumber, int minSold, string showNoOrders, int daysBack, int? minPrice, int? maxPrice)
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

                // group by title and price
                var x = from a in results
                        group a by new { a.Title, a.Price } into grp
                        select new
                              {
                                grp.Key.Title,
                                Price = Convert.ToDecimal(grp.Key.Price),
                                Qty = grp.Sum(s => Convert.ToInt32(s.Qty)),
                                MaxDate = grp.Max(s => Convert.ToDateTime(s.DateOfPurchase)),
                                Url = grp.Max(s => s.Url),
                                ImageUrl = grp.Max(s => s.ImageUrl)
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

                if (minPrice.HasValue)
                    x = x.Where(u => u.Price >= minPrice);

                if (maxPrice.HasValue)
                    x = x.Where(u => u.Price <= maxPrice);

                // group by title, url and price
                // (eventually provide this report)
                // may also just group by title for differing prices

                //var x = from a in results
                //        group a by new { a.Title, a.Url, a.Price, a.ImageUrl } into grp
                //        select new
                //        {
                //            grp.Key.Title,
                //            grp.Key.Url,
                //            grp.Key.Price,
                //            grp.Key.ImageUrl,
                //            Qty = grp.Sum(s => Convert.ToInt32(s.Qty)),
                //            MaxDate = grp.Max(s => Convert.ToDateTime(s.DateOfPurchase))
                //        } into g
                //        where g.Qty >= minSold
                //        orderby g.MaxDate descending
                //        select new TimesSold
                //        {
                //            Title = g.Title,
                //            Url = g.Url,
                //            ImageUrl = g.ImageUrl,
                //            Price = g.Price,
                //            SoldQty = g.Qty,
                //            EarliestSold = g.MaxDate
                //        };

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
                string msg = " GetTimesSold " + exc.Message;
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
    }
}