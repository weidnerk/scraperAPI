﻿/*
 * Note there are 2 Web references: com.ebay.developer and com.ebay.developer1 - they are not duplicate references:
 * 
 *      com.ebay.developer is a reference to the Finding API
 *      com.ebay.developer1 is a reference to the Shopping API (GetSingleItem)
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
using System.Collections.Generic;
using System.Web.Http.Results;
using dsmodels;

namespace scrapeAPI.Controllers
{
    [Authorize]
    public class ScraperController : ApiController
    {
        dsmodels.DataModelsDB db = new dsmodels.DataModelsDB();
        Models.DataModelsDB models = new Models.DataModelsDB();

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

        [Route("getsearchhistory")]
        public IHttpActionResult GetSearchHistory()
        {
            return Ok(models.SearchHistory.OrderByDescending(x => x.Updated));
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
                dsutil.DSUtil.WriteFile(_logfile, msg);
                return BadRequest(msg);
            }
        }

        [Route("numitemssold")]
        [HttpGet]
        public async Task<IHttpActionResult> GetNumItemsSold(string seller, int daysBack, int resultsPerPg, int minSold, string userName)
        {
            try
            {
                // stub to delete a user
                //AccountController.DeleteUsr("ventures2021@gmail.com");
                //AccountController.DeleteUsr("aaronmweidner@gmail.com");
                var user = await UserManager.FindByNameAsync(userName);

                var sh = new SearchHistory();
                sh.UserId = user.Id;
                sh.Seller = seller;
                sh.DaysBack = daysBack;
                sh.MinSoldFilter = minSold;
                var sh_updated = await db.SearchHistorySave(sh);

                int itemCount = ebayAPIs.ItemCount(seller, daysBack, user, sh_updated.Id);
                var mv = new ModelView();
                mv.ItemCount = itemCount;
                mv.ReportNumber = sh_updated.Id;
                return Ok(mv);
            }
            catch (Exception exc)
            {
                string msg = " GetNumItemsSold " + exc.Message;
                dsutil.DSUtil.WriteFile(_logfile, msg);
                return BadRequest(msg);
            }
        }

        [Route("getsellersold")]
        [HttpGet]
        public async Task<IHttpActionResult> FetchSeller(string seller, int daysBack, int resultsPerPg, int minSold, string userName, int reportNumber)
        {
            try
            {
                string header = string.Format("Seller: {0} daysBack: {1} resultsPerPg: {2}", seller, daysBack, resultsPerPg);
                dsutil.DSUtil.WriteFile(_logfile, header);

                var user = await UserManager.FindByNameAsync(userName);

                ebayAPIs.GetAPIStatus(user);

                var mv = await GetSellerSoldAsync(seller, daysBack, resultsPerPg, reportNumber, minSold, user);
                return Ok(mv);
            }
            catch (Exception exc)
            {
                string msg = " FetchSeller " + exc.Message;
                dsutil.DSUtil.WriteFile(_logfile, msg);
                return BadRequest(msg);
            }
        }

        // Recall FindCompletedItems will only give us up to 100 listings
        // interesting case is 'fabulousfinds101' - 
        protected async Task<ModelView> GetSellerSoldAsync(string seller, int daysBack, int resultsPerPg, int rptNumber, int minSold, ApplicationUser user)
        {
            HttpResponseMessage message = Request.CreateResponse<ModelView>(HttpStatusCode.NoContent, null);
            var profile = db.GetUserProfile(user.Id);
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

                var results = (from c in models.SellerOrderHistory
                               where c.RptNumber == rptNumber && c.DateOfPurchase >= ModTimeFrom // && c.ItemId == "352518109311"
                               select c
                               ).ToList();

                // group by title and price and filter by minSold
                var x = from a in results
                        group a by new { a.Title, a.SellerPrice, a.ItemId } into grp
                        select new
                        {
                            grp.Key.Title,
                            Price = Convert.ToDecimal(grp.Key.SellerPrice),
                            Qty = grp.Sum(s => Convert.ToInt32(s.Qty)),
                            MaxDate = grp.Max(s => Convert.ToDateTime(s.DateOfPurchase)),
                            Url = grp.Max(s => s.EbayUrl),
                            ImageUrl = grp.Max(s => s.ImageUrl),
                            ItemId = grp.Max(s => s.ItemId),
                            SellingState = grp.Max(s => s.SellingState),
                            ListingStatus = grp.Max(s => s.ListingStatus),
                            Listed = grp.Max(s => s.Listed)
                        } into g
                              where g.Qty >= minSold
                              orderby g.MaxDate descending
                        select new TimesSold
                              {
                                  Title = g.Title,
                                  EbayUrl = g.Url,
                                  ImageUrl = g.ImageUrl,
                                  SupplierPrice = g.Price,
                                  SoldQty = g.Qty,
                                  EarliestSold = g.MaxDate,
                                  ItemId = g.ItemId,
                                  SellingState = g.SellingState,
                                  ListingStatus = g.ListingStatus,
                                  Listed = g.Listed
                        };

                // filter by min and max price
                if (minPrice.HasValue)
                    x = Enumerable.Where<TimesSold>((IEnumerable<TimesSold>)x, (Func<TimesSold, bool>)(u => (bool)(u.SupplierPrice >= minPrice)));

                if (maxPrice.HasValue)
                    x = Enumerable.Where<TimesSold>((IEnumerable<TimesSold>)x, (Func<TimesSold, bool>)(u => (bool)(u.SupplierPrice <= maxPrice)));

                // count listings processed so far
                var listings = from o in models.SellerOrderHistory
                               where o.RptNumber == rptNumber
                               group o by new { o.Title } into grp
                               select grp;

                // count listings processed so far - matches
                var matchedlistings = from c in results
                                      join o in models.SellerOrderHistory on c.Title equals o.Title
                                      where o.RptNumber == rptNumber && !o.ListingEnded
                                      group c by new { c.Title, c.EbayUrl, o.RptNumber, c.ImageUrl } into grp
                                      select grp;

                // count orders processed so far - matches
                var orders = from c in results
                             join o in models.SellerOrderHistory on c.Title equals o.Title
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
                dsutil.DSUtil.WriteFile(_logfile, msg);
                return BadRequest(msg);
            }
        }

        // get source and ebay images
        [HttpGet]
        [Route("compareimages")]
        public IHttpActionResult GetImages(int categoryId)
        {
            try
            {
                //var p = db.SearchResults.Where(r => r.CategoryId == categoryId).ToList();
                var p = models.GetSearchReport(categoryId).OrderBy(x => x.SourceItemNo).ToList();

                //var result = db.ItemImages.Where(r => r.CategoryId == categoryId).OrderBy(x => x.SourceItemNo).ToList();
                foreach (SearchReport rec in p)
                {
                    var i = dsutil.DSUtil.DelimitedToList(rec.SourceImgUrl, ';');
                    rec.EbayImgCount = i.Count();
                }
                return Ok(p);
            }
            catch (Exception exc)
            {
                string msg = Util.GetErrMsg(exc);
                return new ResponseMessageResult(Request.CreateErrorResponse(HttpStatusCode.InternalServerError, msg));
            }
        }

        [HttpGet]
        [Route("getappids")]
        public async Task<IHttpActionResult> GetAppIds(string username)
        {
            try
            {
                var user = await UserManager.FindByNameAsync(username);
                var p = db.GetAppIDs(user.Id);

                return Ok(p);
            }
            catch (Exception exc)
            {
                string msg = Util.GetErrMsg(exc);
                return new ResponseMessageResult(Request.CreateErrorResponse(HttpStatusCode.InternalServerError, msg));
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
                dsutil.DSUtil.WriteFile(_logfile, msg);
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
                dsutil.DSUtil.WriteFile(_logfile, msg);
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
                dsutil.DSUtil.WriteFile(_logfile, msg);
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
                dsutil.DSUtil.WriteFile(_logfile, msg);
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
                    var profile = db.GetUserProfile(user.Id);
                    var i = await ebayAPIs.GetSingleItem(itemId, profile.AppID);
                    return Ok(i);
                }
            }
            catch (Exception exc)
            {
                string msg = "GetTokenStatus: " + exc.Message;
                dsutil.DSUtil.WriteFile(_logfile, msg);
                return BadRequest(msg);
            }
        }

        [HttpPost]
        [Route("storelisting")]
        public async Task<IHttpActionResult> StoreListing(ListingX listing)
        {
            try
            {
                listing.Qty = 1;
                await db.ListingSave(listing);
                return Ok();
            }
            catch (Exception exc)
            {
                string msg = dsutil.DSUtil.ErrMsg("StoreListing", exc);
                dsutil.DSUtil.WriteFile(_logfile, msg);
                return BadRequest(msg);
            }
        }

        [HttpPost]
        [Route("storepostedlisting")]
        public async Task<IHttpActionResult> StorePostedListing(ListingX listing)
        {
            try
            {
                await db.PostedListingSaveAsync(listing);
                return Ok();
            }
            catch (Exception exc)
            {
                string msg = dsutil.DSUtil.ErrMsg("StorePostedListing", exc);
                dsutil.DSUtil.WriteFile(_logfile, msg);
                return BadRequest(msg);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="itemId">ebay seller listing id</param>
        /// <returns></returns>
        [HttpGet]
        [Route("createlisting")]
        public async Task<IHttpActionResult> CreateListing(string itemId)
        {
            try
            {
                var errors = await ListingCreateAsync(itemId);
                if (errors != null)
                {
                    var errStr = Util.ListToDelimited(errors.ToArray(), ';');
                    return BadRequest(errStr);
                }
                else
                    return Ok();
            }
            catch (Exception exc)
            {
                string msg = dsutil.DSUtil.ErrMsg("CreateListing", exc);
                dsutil.DSUtil.WriteFile(_logfile, msg);
                return BadRequest(msg);
            }
        }

        [HttpGet]
        [Route("removelisting")]
        public async Task<IHttpActionResult> RemoveListing(string ebayItemId)
        {
            try
            {
                var item = db.Listings.Single(r => r.ListedItemID == ebayItemId);
                ebayAPIs.EndFixedPriceItem(item.ListedItemID);

                await db.UpdateRemovedDate(item);
                return Ok();
            }
            catch (Exception exc)
            {
                return new ResponseMessageResult(Request.CreateErrorResponse(HttpStatusCode.InternalServerError, exc.Message));
            }
        }

        [HttpGet]
        [Route("createpostedlisting")]
        public async Task<IHttpActionResult> CreatePostedListing(string itemId)
        {
            try
            {
                var errors = await PostedListingCreateAsync(itemId);
                if (errors.Count == 0)
                    return Ok();
                else
                {
                    var errStr = Util.ListToDelimited(errors.ToArray(), ';');
                    return BadRequest(errStr);
                }
            }
            catch (Exception exc)
            {
                string msg = dsutil.DSUtil.ErrMsg("CreatePostedListing", exc);
                dsutil.DSUtil.WriteFile(_logfile, msg);
                return BadRequest(msg);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="itemId">ebay seller listing id</param>
        /// <returns></returns>
        protected async Task<List<string>> ListingCreateAsync(string itemId)
        {
            var errors = new List<string>();
            var listing = await db.ListingXGet(itemId);     // item has to be stored before it can be listed
            if (listing != null)
            {
                // if item is listed already, then revise
                if (listing.Listed == null)
                {
                    List<string> pictureURLs = Util.DelimitedToList(listing.PictureUrl, ';');
                    string verifyItemID = eBayItem.VerifyAddItemRequest(listing.ListingTitle,
                        listing.Description,
                        listing.PrimaryCategoryID,
                        (double)listing.ListingPrice,
                        pictureURLs,
                        ref errors,
                        2);

                    // might get warnings and still get a listing item number
                    if (errors.Count == 0)
                    {
                    }
                    if (!string.IsNullOrEmpty(verifyItemID))
                    {
                        if (!listing.Listed.HasValue)
                        {
                            listing.Listed = DateTime.Now;
                        }
                        await db.UpdateListedItemID(listing, verifyItemID);
                    }
                }
                else
                {
                    ebayAPIs.ReviseItem(listing.ListedItemID, 
                                        qty: listing.Qty, 
                                        price: Convert.ToDouble(listing.ListingPrice), 
                                        title: listing.ListingTitle);
                }
            }
            return errors;
        }

        // itemId is id of the ebay seller's listing
        protected async Task<List<string>> PostedListingCreateAsync(string itemId)
        {
            var errors = new List<string>();
            var listing = await db.GetPostedListing(itemId);
            if (listing != null)
            {
                List<string> pictureURLs = Util.DelimitedToList(listing.PictureUrl, ';');
                string verifyItemID = eBayItem.VerifyAddItemRequest(listing.Title,
                    listing.Description,
                    listing.PrimaryCategoryID,
                    (double)listing.ListingPrice,
                    pictureURLs,
                    ref errors,
                    listing.Qty);

                // might get warnings and still get a listing item number
                if (errors.Count == 0)
                {
                }
                if (!string.IsNullOrEmpty(verifyItemID))
                {
                    if (!listing.Listed.HasValue)
                    {
                        listing.Listed = DateTime.Now;
                    }
                    await db.UpdateListedItemID(listing, verifyItemID);
                }
            }
            return errors;
        }

        // return errors
        public async Task<List<string>> PostedListingCreateAsync(StagedListing staged)
        {
            //dsmodels.DataModelsDB db = new dsmodels.DataModelsDB();
            //Models.DataModelsDB models = new Models.DataModelsDB();

            var errors = new List<string>();
            var listing = await db.GetPostedListing(staged.SourceID, staged.SupplierItemID);
            if (listing != null)
            {
                List<string> pictureURLs = Util.DelimitedToList(listing.PictureUrl, ';');
                string verifyItemID = eBayItem.VerifyAddItemRequest(listing.Title,
                    listing.Description,
                    listing.PrimaryCategoryID,
                    (double)listing.ListingPrice,
                    pictureURLs,
                    ref errors,
                    listing.Qty);

                // might get warnings and still get a listing item number
                if (errors.Count == 0)
                {
                }
                if (!string.IsNullOrEmpty(verifyItemID))
                {
                    staged.ListedItemID = verifyItemID;
                    if (!listing.Listed.HasValue)
                    {
                        listing.Listed = DateTime.Now;
                    }
                    await db.UpdateListedItemID(listing, verifyItemID);
                }
            }
            return errors;
        }

        [HttpGet]
        [Route("getlisting")]
        public async Task<IHttpActionResult> GetListing(string itemId)
        {
            try
            {
                var listing = await db.ListingGet(itemId);
                if (listing == null)
                    return NotFound();
                return Ok(listing);
            }
            catch (Exception exc)
            {
                string msg = dsutil.DSUtil.ErrMsg("GetListing", exc);
                dsutil.DSUtil.WriteFile(_logfile, msg);
                return BadRequest(msg);
            }
        }

        [HttpGet]
        [Route("getlistingx")]
        public async Task<IHttpActionResult> GetListingX(string itemId)
        {
            try
            {
                var listing = await db.ListingXGet(itemId);
                if (listing == null)
                    return NotFound();
                return Ok(listing);
            }
            catch (Exception exc)
            {
                string msg = dsutil.DSUtil.ErrMsg("GetListingX", exc);
                dsutil.DSUtil.WriteFile(_logfile, msg);
                return BadRequest(msg);
            }
        }

        [HttpGet]
        [Route("getitem")]
        public IHttpActionResult GetItem(string ebayItemId, string ebayPrice, int categoryId, string shippingAmt)
        {
            try
            {
                decimal price = Convert.ToDecimal(ebayPrice);
                decimal shippingAmount = Convert.ToDecimal(shippingAmt);
                var item = models.GetSearchReport(categoryId).Single(r => r.EbayItemId == ebayItemId && r.EbaySellerPrice == price && r.CategoryId == categoryId && r.ShippingAmount == shippingAmount);
                if (item == null)
                    return NotFound();
                return Ok(item);
            }
            catch (Exception exc)
            {
                string msg = dsutil.DSUtil.ErrMsg("GetItem", exc);
                dsutil.DSUtil.WriteFile(_logfile, msg);
                return BadRequest(msg);
            }
        }

        [HttpGet]
        [Route("getpostedlisting")]
        public IHttpActionResult GetPostedListing(string ebayItemId)
        {
            try
            {
                // a seller may have sold his item at different prices
                var item = db.Listings.SingleOrDefault(r => r.ListedItemID == ebayItemId);
                if (item == null)
                    return NotFound();
                return Ok(item);
            }
            catch (Exception exc)
            {
                string msg = dsutil.DSUtil.ErrMsg("GetPostedListing", exc);
                dsutil.DSUtil.WriteFile(_logfile, msg);
                return BadRequest(msg);
            }
        }

        [HttpGet]
        [Route("getcategories")]
        public IHttpActionResult GetCategories(int sourceId)
        {
            try
            {
                return Ok(db.SourceCategories.Where(r => r.SourceID == sourceId).OrderBy(o => o.SubCategory).ToList());
            }
            catch (Exception exc)
            {
                string msg = dsutil.DSUtil.ErrMsg("GetCategories", exc);
                dsutil.DSUtil.WriteFile(_logfile, msg);
                return BadRequest(msg);
            }
        }

        [HttpDelete]
        [Route("deletescan/{rptNumber}")]
        [AcceptVerbs("DELETE")]
        public async Task<IHttpActionResult> DeleteScan(int rptNumber)
        {
            try
            {
               await db.HistoryRemove(rptNumber);
                return Ok();
            }
            catch (Exception exc)
            {
                string msg = exc.Message;
                return Content(HttpStatusCode.InternalServerError, msg);
            }
        }
    }
}