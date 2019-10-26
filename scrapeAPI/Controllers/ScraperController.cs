/*
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
using Microsoft.AspNet.Identity;
using System.Data.Entity.SqlServer;
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

        /// <summary>
        /// Get user's searches.
        /// </summary>
        /// <param name="userName"></param>
        /// <returns></returns>
        [Route("getsearchhistory")]
        public async Task<IHttpActionResult> GetSearchHistory(string userName)
        {
            try
            {
                var user = await UserManager.FindByNameAsync(userName);
                var sh = models.SearchHistory.Where(p => p.UserId == user.Id).OrderByDescending(x => x.Updated);
                return Ok(sh);
            }
            catch (Exception exc)
            {
                string msg = " GetSearchHistory " + exc.Message;
                dsutil.DSUtil.WriteFile(_logfile, msg, userName);
                return BadRequest(msg);
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
                var settings = db.UserSettingsView.Find(user.Id);
                var response = ebayAPIs.FindByKeyword(settings);
                return Ok(response);
            }
            catch (Exception exc)
            {
                string msg = " GetProdById " + exc.Message;
                dsutil.DSUtil.WriteFile(_logfile, msg, userName);
                return BadRequest(msg);
            }
        }

        /// <summary>
        /// Store the search result in SearchHistory and use FindingService to get a count.
        /// </summary>
        /// <param name="seller"></param>
        /// <param name="daysBack"></param>
        /// <param name="resultsPerPg"></param>
        /// <param name="minSold"></param>
        /// <param name="userName"></param>
        /// <returns></returns>
        [Route("numitemssold")]
        [HttpGet]
        public async Task<IHttpActionResult> GetNumItemsSold(string seller, int daysBack, int resultsPerPg, int minSold, string userName)
        {
            string msg = null;
            try
            {
                // stub to delete a user
                //AccountController.DeleteUsr("ventures2021@gmail.com");
                //AccountController.DeleteUsr("aaronmweidner@gmail.com");
                var user = await UserManager.FindByNameAsync(userName);
                var settings = db.UserSettingsView.Find(user.Id);

                var r = db.CanRunScan(user.Id, seller);
                if (!r)
                {
                    msg = "Cannot scan seller";
                    return BadRequest(msg);
                }

                var sh = new SearchHistory();
                sh.UserId = user.Id;
                sh.Seller = seller;
                sh.DaysBack = daysBack;
                sh.MinSoldFilter = minSold;
                sh.StoreID = settings.StoreID;
                var sh_updated = await db.SearchHistoryAdd(sh);

                int itemCount = ebayAPIs.ItemCount(seller, daysBack, settings);
                var mv = new ModelView();
                mv.ItemCount = itemCount;
                mv.ReportNumber = sh_updated.Id;

                return Ok(mv);
            }
            catch (Exception exc)
            {
                msg = " GetNumItemsSold " + exc.Message;
                dsutil.DSUtil.WriteFile(_logfile, msg, userName);
                return BadRequest(msg);
            }
        }

        /// <summary>
        /// Init scan of seller.
        /// </summary>
        /// <param name="seller"></param>
        /// <param name="daysBack"></param>
        /// <param name="resultsPerPg"></param>
        /// <param name="minSold"></param>
        /// <param name="userName"></param>
        /// <param name="reportNumber"></param>
        /// <returns></returns>
        [Route("getsellersold")]
        [HttpGet]
        public async Task<IHttpActionResult> FetchSeller(string seller, int daysBack, int resultsPerPg, int minSold, string userName, int reportNumber)
        {
            try
            {
                string header = string.Format("Seller: {0} daysBack: {1} resultsPerPg: {2}", seller, daysBack, resultsPerPg);
                dsutil.DSUtil.WriteFile(_logfile, header, userName);

                var user = await UserManager.FindByNameAsync(userName);
                var settings = db.UserSettingsView.Find(user.Id);
                ebayAPIs.GetAPIStatus(settings);

                var mv = await ebayAPIs.ToStart(seller, daysBack, settings, reportNumber);   // scan the seller

                var sh = db.SearchHistory.Where(p => p.Id == reportNumber).FirstOrDefault();
                sh.Running = false;
                await db.SearchHistoryUpdate(sh);

                return Ok(mv);
            }
            catch (Exception exc)
            {
                string msg = " FetchSeller " + exc.Message;
                dsutil.DSUtil.WriteFile(_logfile, msg, userName);
                return BadRequest(msg);
            }
        }

        [Route("cancelscan/{rptNumber}")]
        [HttpGet]
        public async Task<IHttpActionResult> CancelScan(int rptNumber)
        {
            var f = db.SearchHistory.Where(p => p.Id == rptNumber).FirstOrDefault();
            if (f != null)
            {
                f.Running = false;
                await db.SearchHistoryUpdate(f);
            }
            return Ok();
        }

        /// <summary>
        /// Get stored scan.
        /// This may be called on a timer to keep feeding results.
        /// </summary>
        /// <param name="rptNumber"></param>
        /// <param name="minSold"></param>
        /// <param name="daysBack"></param>
        /// <param name="minPrice"></param>
        /// <param name="maxPrice"></param>
        /// <returns></returns>
        [Route("getreport/{rptNumber}/{minSold}/{daysBack}/{minPrice}/{maxPrice}/{activeStatusOnly}/{nonVariation}")]
        [HttpGet]
        public IHttpActionResult GetReport(int rptNumber, int minSold, int daysBack, int? minPrice, int? maxPrice, bool activeStatusOnly = false, bool nonVariation = false)
        {
            string strCurrentUserId = User.Identity.GetUserId();
            var settings = db.UserSettingsView.Find(strCurrentUserId);

            //bool endedListings = (showNoOrders == "0") ? false : true;
            DateTime ModTimeTo = DateTime.Now.ToUniversalTime();
            DateTime ModTimeFrom = ModTimeTo.AddDays(-daysBack);

            try
            {
                string msg = "start GetReport ";
                dsutil.DSUtil.WriteFile(_logfile, msg, "nousername");

                var x = models.GetScanData(rptNumber, ModTimeFrom, settings.StoreID);

                // filter by min and max price
                if (minPrice.HasValue)
                {
                    x = x.Where(p => p.SellerPrice >= minPrice);
                }
                if (maxPrice.HasValue)
                {
                    x = x.Where(p => p.SellerPrice <= maxPrice);
                }
                x = x.Where(p => p.SoldQty >= minSold);
                if (activeStatusOnly)
                {
                    x = x.Where(p => p.ListingStatus == "Active");
                }
                if (nonVariation)
                {
                    x = x.Where(p => !p.IsMultiVariationListing.Value);
                }
                x = x.OrderByDescending(p => p.LatestSold);
                var mv = new ModelViewTimesSold();
                mv.TimesSoldRpt = x.ToList();
                mv.ListingsProcessed = 0;
                mv.TotalOrders = 0;
                mv.ItemCount = mv.TimesSoldRpt.Count;

                msg = "end GetReport ";
                dsutil.DSUtil.WriteFile(_logfile, msg, "nousername");

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
                dsutil.DSUtil.WriteFile(_logfile, msg, "nousername");
                return BadRequest(msg);
            }
        }

        protected void GetScanReport()
        {

        }

        /// <summary>
        /// get source and ebay images 
        /// </summary>
        /// <param name="categoryId"></param>
        /// <returns></returns>
        [HttpGet]
        [Route("compareimages")]
        public IHttpActionResult GetImages(int categoryId)
        {
            try
            {
                var p = models.GetSearchReport(categoryId).OrderBy(x => x.SourceItemNo).ToList();

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

        /// <summary>
        /// 
        /// </summary>
        /// <param name="username"></param>
        /// <returns></returns>
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
        [AllowAnonymous]
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
                dsutil.DSUtil.WriteFile(_logfile, msg, "nousername");
                return BadRequest(msg);
            }
        }
        [AllowAnonymous]
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
                dsutil.DSUtil.WriteFile(_logfile, msg, username);
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
                var settings = db.UserSettingsView.Find(user.Id);
                if (user == null)
                    return Ok(false);
                else
                {
                    var i = ebayAPIs.GetTradingAPIUsage(settings);
                    return Ok(i);
                }
            }
            catch (Exception exc)
            {
                string msg = "GetTradingAPIUsage: " + exc.Message;
                dsutil.DSUtil.WriteFile(_logfile, msg, userName);
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
                var settings = db.UserSettingsView.Find(user.Id);
                if (user == null)
                    return Ok(false);
                else
                {
                    var i = ebayAPIs.GetTokenStatus(settings);
                    return Ok(i);
                }
            }
            catch (Exception exc)
            {
                string msg = "GetTokenStatus: " + exc.Message;
                dsutil.DSUtil.WriteFile(_logfile, msg, userName);
                return BadRequest(msg);
            }
        }

        [HttpGet]
        [Route("getsellerlisting")]
        public async Task<IHttpActionResult> GetSellerListing(string userName, string itemId)
        {
            try
            {
                var user = await UserManager.FindByNameAsync(userName);
                if (user == null)
                    return Ok(false);
                else
                {
                    var settings = db.UserSettingsView.Find(user.Id);
                    var i = await ebayAPIs.GetSingleItem(itemId, settings.AppID);
                    return Ok(i);
                }
            }
            catch (Exception exc)
            {
                string msg = "GetTokenStatus: " + exc.Message;
                dsutil.DSUtil.WriteFile(_logfile, msg, "nousername");
                return BadRequest(msg);
            }
        }

        [HttpPost]
        [Route("storelisting")]
        public async Task<IHttpActionResult> StoreListing(Listing listing)
        {
            try
            {
                string strCurrentUserId = User.Identity.GetUserId();
                var settings = db.UserSettingsView.Find(strCurrentUserId);
                listing.StoreID = settings.StoreID;
                listing.Qty = 1;
                await db.ListingSave(listing);
                return Ok();
            }
            catch (Exception exc)
            {
                string msg = dsutil.DSUtil.ErrMsg("StoreListing", exc);
                dsutil.DSUtil.WriteFile(_logfile, msg, "nousername");
                return BadRequest(msg);
            }
        }
        [HttpPost]
        [Route("storenote")]
        public async Task<IHttpActionResult> StoreNote(Listing listing)
        {
            try
            {
                string strCurrentUserId = User.Identity.GetUserId();
                await db.NoteSave(listing);
                return Ok();
            }
            catch (Exception exc)
            {
                string msg = dsutil.DSUtil.ErrMsg("StoreNote", exc);
                dsutil.DSUtil.WriteFile(_logfile, msg, "nousername");
                return BadRequest(msg);
            }
        }
        [HttpPost]
        [Route("storeoos")]
        public async Task<IHttpActionResult> StoreOOS(Listing listing)
        {
            try
            {
                string strCurrentUserId = User.Identity.GetUserId();
                await db.OOSSave(listing);
                return Ok();
            }
            catch (Exception exc)
            {
                string msg = dsutil.DSUtil.ErrMsg("StoreOOS", exc);
                dsutil.DSUtil.WriteFile(_logfile, msg, "nousername");
                return BadRequest(msg);
            }
        }
        [HttpPost]
        [Route("storesellerprofile")]
        public async Task<IHttpActionResult> StoreSellerProfile(SellerProfile sellerProfile)
        {
            try
            {
                string strCurrentUserId = User.Identity.GetUserId();
                sellerProfile.UpdatedBy = strCurrentUserId;
                sellerProfile.UserID = strCurrentUserId;
                await db.SellerProfileSave(sellerProfile);
                return Ok();
            }
            catch (Exception exc)
            {
                string msg = dsutil.DSUtil.ErrMsg("StoreListing", exc);
                dsutil.DSUtil.WriteFile(_logfile, msg, "nousername");
                return BadRequest(msg);
            }
        }

        [HttpPost]
        [Route("storepostedlisting")]
        public async Task<IHttpActionResult> StorePostedListing(Listing listing)
        {
            try
            {
                await db.PostedListingSaveAsync(listing);
                return Ok();
            }
            catch (Exception exc)
            {
                string msg = dsutil.DSUtil.ErrMsg("StorePostedListing", exc);
                dsutil.DSUtil.WriteFile(_logfile, msg, "nousername");
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
                string strCurrentUserId = User.Identity.GetUserId();
                var settings = db.UserSettingsView.Find(strCurrentUserId);
                var output = await ListingCreateAsync(settings, itemId);
                if (ListingNotCreated(output))
                {
                    var errStr = Util.ListToDelimited(output.ToArray(), ';');
                    return BadRequest(errStr);
                }
                else
                {
                    var str = Util.ListToDelimited(output.ToArray(), ';');
                    return Ok(str);   // return listing id
                }
            }
            catch (Exception exc)
            {
                string msg = dsutil.DSUtil.ErrMsg("CreateListing", exc);
                dsutil.DSUtil.WriteFile(_logfile, msg, "nousername");
                return BadRequest(msg);
            }
        }

        protected static bool ListingNotCreated(List<string> response)
        {
            const string marker = "Listing not created";
            foreach (string s in response)
            {
                if (s.Contains(marker))
                {
                    return true;
                }
            }
            return false;
        }

        [HttpGet]
        [Route("removelisting")]
        public async Task<IHttpActionResult> RemoveListing(UserSettingsView settings, string ebayItemId)
        {
            try
            {
                var item = db.Listings.Single(r => r.ListedItemID == ebayItemId);
                ebayAPIs.EndFixedPriceItem(settings, item.ListedItemID);

                await db.UpdateRemovedDate(item);
                return Ok();
            }
            catch (Exception exc)
            {
                return new ResponseMessageResult(Request.CreateErrorResponse(HttpStatusCode.InternalServerError, exc.Message));
            }
        }

        [HttpGet]
        [Route("setorder")]
        public IHttpActionResult SetOrder(Listing listing)
        {
            try
            {
                // ebayAPIs.GetOrders("19-04026-11927");
                return Ok();
            }
            catch (Exception exc)
            {
                return new ResponseMessageResult(Request.CreateErrorResponse(HttpStatusCode.InternalServerError, exc.Message));
            }
        }

        //[HttpGet]
        //[Route("createpostedlisting")]
        //public async Task<IHttpActionResult> CreatePostedListing(string itemId)
        //{
        //    try
        //    {
        //        var errors = await PostedListingCreateAsync(itemId);
        //        if (errors.Count == 0)
        //            return Ok();
        //        else
        //        {
        //            var errStr = Util.ListToDelimited(errors.ToArray(), ';');
        //            return BadRequest(errStr);
        //        }
        //    }
        //    catch (Exception exc)
        //    {
        //        string msg = dsutil.DSUtil.ErrMsg("CreatePostedListing", exc);
        //        dsutil.DSUtil.WriteFile(_logfile, msg, "nousername");
        //        return BadRequest(msg);
        //    }
        //}

        /// <summary>
        /// 
        /// </summary>
        /// <param name="itemId">ebay seller listing id</param>
        /// <returns></returns>
        protected async Task<List<string>> ListingCreateAsync(UserSettingsView settings, string itemId)
        {
            var output = new List<string>();
            
            var listing = await db.ListingGet(itemId);     // item has to be stored before it can be listed
            if (listing != null)
            {
                // if item is listed already, then revise
                if (listing.Listed == null)
                {
                    List<string> pictureURLs = Util.DelimitedToList(listing.PictureUrl, ';');
                    string verifyItemID = eBayItem.VerifyAddItemRequest(settings, listing.ListingTitle,
                        listing.Description,
                        listing.PrimaryCategoryID,
                        (double)listing.ListingPrice,
                        pictureURLs,
                        ref output,
                        2,  
                        listing);
                    // at this point, 'output' will be populated with errors if any occurred

                    if (!string.IsNullOrEmpty(verifyItemID))
                    {
                        output.Insert(0, verifyItemID);
                        output.Insert(0, "Listed: YES - check listing's images and description");
                        if (!listing.Listed.HasValue)
                        {
                            listing.Listed = DateTime.Now;
                        }
                        var response = FlattenList(output);
                        await db.UpdateListedItemID(listing, verifyItemID, settings.UserID, true, response);
                    }
                    else
                    {
                        output.Add("Listing not created.");
                    }
                }
                else
                {
                    string response = null;
                    output = ebayAPIs.ReviseItem(settings, listing.ListedItemID,
                                        qty: listing.Qty,
                                        price: Convert.ToDouble(listing.ListingPrice),
                                        title: listing.ListingTitle);
                    if (output.Count > 0)
                    {
                        response = FlattenList(output);
                    }
                    await db.UpdateListedItemID(listing, listing.ListedItemID, settings.UserID, true, response, updated: DateTime.Now);
                    output.Insert(0, listing.ListedItemID);
                }
            }
            return output;
        }
        protected static string FlattenList(List<string> errors)
        {
            string output = null;
            foreach (string s in errors) {
                output += s + ";";
            }
            var r = output.Substring(0, output.Length - 1);
            return r;
        }

        [HttpGet]
        [Route("getlisting")]
        public async Task<IHttpActionResult> GetListing(string userName, string itemId)
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
                dsutil.DSUtil.WriteFile(_logfile, msg, "nousername");
                return BadRequest(msg);
            }
        }

        [HttpGet]
        [Route("getsellerprofile")]
        public async Task<IHttpActionResult> GetSellerProfile(string userName, string seller)
        {
            try
            {
                var sellerProfile = await db.SellerProfileGet(seller);
                if (sellerProfile == null)
                    return NotFound();
                return Ok(sellerProfile);
            }
            catch (Exception exc)
            {
                string msg = dsutil.DSUtil.ErrMsg("GetListing", exc);
                dsutil.DSUtil.WriteFile(_logfile, msg, "nousername");
                return BadRequest(msg);
            }
        }

        [HttpGet]
        [Route("getlistings")]
        public IHttpActionResult GetListings()
        {
            try
            {
                string strCurrentUserId = User.Identity.GetUserId();
                var settings = db.UserSettingsView.Find(strCurrentUserId);

                var listings = db.GetListings(settings.StoreID);
                if (listings == null)
                    return NotFound();
                return Ok(listings);
            }
            catch (Exception exc)
            {
                string msg = dsutil.DSUtil.ErrMsg("GetListings", exc);
                dsutil.DSUtil.WriteFile(_logfile, msg, "nousername");
                return BadRequest(msg);
            }
        }

        [HttpGet]
        [Route("getwmitem")]
        public async Task<IHttpActionResult> GetWMItem(string userName, string url)
        {
            try
            {
                var w = await wallib.Class1.GetDetail(url);
                return Ok(w);
            }
            catch (Exception exc)
            {
                string msg = dsutil.DSUtil.ErrMsg("GetWMItem", exc);
                dsutil.DSUtil.WriteFile(_logfile, msg, "nousername");
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
                dsutil.DSUtil.WriteFile(_logfile, msg, "nousername");
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
                dsutil.DSUtil.WriteFile(_logfile, msg, "nousername");
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
                dsutil.DSUtil.WriteFile(_logfile, msg, "nousername");
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
                dsutil.DSUtil.WriteFile(_logfile, msg, "nousername");
                return Content(HttpStatusCode.InternalServerError, msg);
            }
        }

        [HttpDelete]
        [Route("deletelistingrecord/{sellerItemId}")]
        [AcceptVerbs("DELETE")]
        public async Task<IHttpActionResult> DeleteListingRecord(string sellerItemId)
        {
            try
            {
                await db.DeleteListingRecord(sellerItemId);
                return Ok();
            }
            catch (Exception exc)
            {
                string msg = exc.Message;
                dsutil.DSUtil.WriteFile(_logfile, msg, "nousername");
                return Content(HttpStatusCode.InternalServerError, msg);
            }
        }
        [HttpGet]
        [Route("dashboard")]
        public IHttpActionResult GetDashboard()
        {
            try
            {
                var dashboard = new Dashboard();
                int OOS = db.Listings.Where(p => p.OOS).Count();
                dashboard.OOS = OOS;

                int notListed = db.Listings.Where(p => p.Listed == null).Count();
                dashboard.NotListed = notListed;

                return Ok(dashboard);
            }
            catch (Exception exc)
            {
                string msg = dsutil.DSUtil.ErrMsg("GetCategories", exc);
                dsutil.DSUtil.WriteFile(_logfile, msg, "nousername");
                return BadRequest(msg);
            }
        }
    }
}