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
using eBay.Service.Core.Soap;
using eBayUtility;
using System.Configuration;
using Utility;
using System.Data.Entity;

namespace scrapeAPI.Controllers
{
    [Authorize]
    public class ScraperController : ApiController
    {
        dsmodels.DataModelsDB db = new dsmodels.DataModelsDB();
        Models.DataModelsDB models = new Models.DataModelsDB();

        const string _filename = "order.csv";
        readonly string _logfile = "log.txt";

        const int _qtyToList = 2;

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
                var sh = db.SearchHistoryView.Where(p => p.UserId == user.Id).OrderByDescending(x => x.Updated).ToList();
                return Ok(sh);
            }
            catch (Exception exc)
            {
                string msg = dsutil.DSUtil.ErrMsg("GetSearchHistory", exc);
                return BadRequest(msg);
            }
        }


        [Route("cancelscan/{rptNumber}")]
        [HttpGet]
        public async Task<IHttpActionResult> CancelScan(int rptNumber)
        {
            string strCurrentUserId = User.Identity.GetUserId();
            string connStr = ConfigurationManager.ConnectionStrings["OPWContext"].ConnectionString;
            var settings = db.GetUserSettings(connStr, strCurrentUserId);

            try
            {
                var f = db.SearchHistory.Where(p => p.ID == rptNumber).FirstOrDefault();
                if (f != null)
                {
                    f.Running = false;
                    await db.SearchHistoryUpdate(f);
                }
                return Ok();
            }
            catch (Exception exc)
            {
                string msg = dsutil.DSUtil.ErrMsg("CancelScan", exc);
                return BadRequest(msg);
            }
        }

        [Route("updatetolist")]
        [HttpPost]
        public async Task<IHttpActionResult> OrderHistoryUpdateToList(OrderHistory oh)
        {
            string strCurrentUserId = User.Identity.GetUserId();
            string connStr = ConfigurationManager.ConnectionStrings["OPWContext"].ConnectionString;
            var settings = db.GetUserSettings(connStr, strCurrentUserId);

            await db.OrderHistorySaveToList(oh);

            return Ok();
        }

        /// <summary>
        /// Get stored scan.
        /// This may be called on a timer to keep feeding results.
        /// </summary>
        /// <param name="rptNumber">Pass 0 if passing itemID</param>
        /// <param name="minSold"></param>
        /// <param name="daysBack"></param>
        /// <param name="minPrice"></param>
        /// <param name="maxPrice"></param>
        /// <param name="activeStatusOnly"></param>
        /// <param name="isSellerVariation"></param>
        /// <param name="itemID">Pass null if passing rptNumber</param>
        /// <returns></returns>
        [Route("getreport/{rptNumber}/{minSold}/{daysBack}/{minPrice}/{maxPrice}/{activeStatusOnly}/{isSellerVariation}/{itemID}/{filter}")]
        [HttpGet]
        public IHttpActionResult GetReport(int rptNumber, int minSold, int daysBack, int? minPrice, int? maxPrice, bool? activeStatusOnly, bool? isSellerVariation, string itemID, int filter)
        {
            string strCurrentUserId = User.Identity.GetUserId();
            string connStr = ConfigurationManager.ConnectionStrings["OPWContext"].ConnectionString;
            var settings = db.GetUserSettings(connStr, strCurrentUserId);

            DateTime ModTimeTo = DateTime.Now.ToUniversalTime();
            DateTime ModTimeFrom = ModTimeTo.AddDays(-daysBack);

            try
            {
                itemID = (itemID == "null") ? null : itemID;

                var x = db.GetScanData(rptNumber, ModTimeFrom, settings.StoreID, itemID: itemID);
                if (minPrice.HasValue)
                {
                    x = x.Where(p => p.Price >= minPrice);
                }
                if (maxPrice.HasValue)
                {
                    x = x.Where(p => p.Price <= maxPrice);
                }
                x = x.Where(p => p.SoldQty >= minSold);
                if (activeStatusOnly.HasValue)
                {
                    if (activeStatusOnly.Value)
                    {
                        x = x.Where(p => p.ListingStatus == "Active");
                    }
                }
                if (isSellerVariation.HasValue)
                {
                    if (isSellerVariation.Value)
                    {
                        x = x.Where(p => !p.IsSellerVariation ?? false);
                    }
                }
                var mv = new ModelViewTimesSold();
                mv.TimesSoldRpt = x.ToList();
                if (filter > 0)
                {
                    mv.TimesSoldRpt = mv.TimesSoldRpt.Where(p => p.MatchCount == 1 && (p.SoldAndShippedBySupplier ?? false)).ToList();
                    if (filter >= 2)
                    {
                        mv.TimesSoldRpt = mv.TimesSoldRpt.Where(p => (!p.IsSupplierVariation ?? false)).ToList();
                    }
                    if (filter >= 3)
                    {
                        var idList = new List<int>();
                        var toExclude = new string[] { "UNBRANDED", "DOES NOT APPLY" };
                        int i = 0;
                        foreach (var item in mv.TimesSoldRpt)
                        {
                            if (!string.IsNullOrEmpty(item.SellerBrand))
                            {
                                if (!toExclude.Contains(item.SellerBrand.ToUpper()))
                                {
                                    if (item.SupplierBrand != null)
                                    {
                                        if (item.SellerBrand.ToUpper() != item.SupplierBrand.ToUpper())
                                        {
                                            idList.Add(i);
                                        }
                                    }
                                }
                            }
                            ++i;
                        }
                        if (idList.Count > 0)
                        {
                            for (int j = idList.Count - 1; j >= 0; j--)
                            {
                                mv.TimesSoldRpt.RemoveAt(idList[j]);
                            }
                        }
                    }
                    if (filter >= 4)
                    {
                        mv.TimesSoldRpt = mv.TimesSoldRpt.Where(p => !db.IsVERO(p.SupplierBrand)).ToList();
                    }
                }
                
                
                foreach (var item in mv.TimesSoldRpt.Where(w => w.SupplierPrice.HasValue))
                {
                    if (item.SupplierPrice > 0)
                    {
                        if (item.SupplierPrice < 35.0m)
                        {
                            item.SellerProfit = item.Price - (item.SupplierPrice + 5.99m);
                        }
                        else
                        {
                            item.SellerProfit = item.Price - item.SupplierPrice;
                        }
                    }
                }
                mv.TimesSoldRpt.ToList().ForEach(c => c.IsVero = db.IsVERO(c.SupplierBrand));
                mv.TimesSoldRpt = mv.TimesSoldRpt.OrderByDescending(p => p.LatestSold).ToList();
                mv.ListingsProcessed = 0;
                mv.TotalOrders = 0;
                mv.ItemCount = mv.TimesSoldRpt.Count;

                return Ok(mv);
            }
            catch (Exception exc)
            {
                string msg = dsutil.DSUtil.ErrMsg("GetReport", exc);
                return BadRequest(msg);
            }
        }

        [Route("fillmatch/{rptNumber}/{minSold}/{daysBack}/{minPrice}/{maxPrice}/{activeStatusOnly}/{nonVariation}/{itemID}")]
        [HttpGet]
        public async Task<IHttpActionResult> FillMatch(int rptNumber, int minSold, int daysBack, int? minPrice, int? maxPrice, bool? activeStatusOnly, bool? nonVariation, string itemID)
        {
            string strCurrentUserId = User.Identity.GetUserId();
            string connStr = ConfigurationManager.ConnectionStrings["OPWContext"].ConnectionString;
            var settings = db.GetUserSettings(connStr, strCurrentUserId);

            var mv = await FetchSeller.FillMatch(settings, rptNumber, minSold, daysBack, minPrice, maxPrice, activeStatusOnly, nonVariation, itemID, 5);
            return Ok(mv);
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
                string msg = dsutil.DSUtil.ErrMsg("GetEmailTaken", exc);
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
                string connStr = ConfigurationManager.ConnectionStrings["OPWContext"].ConnectionString;
                var settings = db.GetUserSettings(connStr, user.Id);

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
                string connStr = ConfigurationManager.ConnectionStrings["OPWContext"].ConnectionString;
                var settings = db.GetUserSettings(connStr, user.Id);

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
        public async Task<IHttpActionResult> GetSellerListing(string userName, string itemID)
        {
            try
            {
                var user = await UserManager.FindByNameAsync(userName);
                if (user == null)
                    return Ok(false);
                else
                {
                    string connStr = ConfigurationManager.ConnectionStrings["OPWContext"].ConnectionString;
                    var settings = db.GetUserSettings(connStr, user.Id);

                    var i = await ebayAPIs.GetSingleItem(settings, itemID);
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
        [HttpGet]
        [Route("storetolisting/{userName}/{rptNumber}")]
        public async Task<IHttpActionResult> StoreToListing(string userName, int rptNumber)
        {
            try
            {
                var user = await UserManager.FindByNameAsync(userName);
                if (user == null)
                    return Ok(false);
                else
                {
                    string connStr = ConfigurationManager.ConnectionStrings["OPWContext"].ConnectionString;
                    var settings = db.GetUserSettings(connStr, user.Id);
                    await eBayUtility.FetchSeller.StoreToListing(settings, rptNumber);
                    return Ok();
                }
            }
            catch (Exception exc)
            {
                string msg = "StoreToListing: " + exc.Message;
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
                string connStr = ConfigurationManager.ConnectionStrings["OPWContext"].ConnectionString;
                var settings = db.GetUserSettings(connStr, strCurrentUserId);

                listing.StoreID = settings.StoreID;
                listing.Qty = _qtyToList;
                await db.ListingSaveAsync(listing, strCurrentUserId);
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
        public async Task<IHttpActionResult> StoreNote(ListingNote note)
        {
            try
            {
                string strCurrentUserId = User.Identity.GetUserId();
                string connStr = ConfigurationManager.ConnectionStrings["OPWContext"].ConnectionString;
                var settings = db.GetUserSettings(connStr, strCurrentUserId);

                note.UserID = strCurrentUserId;
                note.StoreID = settings.StoreID;
                await db.NoteSave(note);
                return Ok();
            }
            catch (Exception exc)
            {
                string msg = dsutil.DSUtil.ErrMsg("StoreNote", exc);
                dsutil.DSUtil.WriteFile(_logfile, msg, "nousername");
                return BadRequest(msg);
            }
        }
        [HttpGet]
        [Route("itemnotes")]
        public async Task<IHttpActionResult> GetItemNotes(string itemID)
        {
            try
            {
                string strCurrentUserId = User.Identity.GetUserId();
                string connStr = ConfigurationManager.ConnectionStrings["OPWContext"].ConnectionString;
                var settings = db.GetUserSettings(connStr, strCurrentUserId);

                var notes = await db.ItemNotes(itemID, settings.StoreID);
                return Ok(notes);
            }
            catch (Exception exc)
            {
                string msg = dsutil.DSUtil.ErrMsg("GetItemNotes", exc);
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

        /// <summary>
        /// 
        /// </summary>
        /// <param name="itemId">ebay seller listing id</param>
        /// <returns></returns>
        [HttpGet]
        [Route("createlisting")]
        public async Task<IHttpActionResult> CreateListing(string itemId)
        {
            var settings = new UserSettingsView();
            try
            {
                string strCurrentUserId = User.Identity.GetUserId();
                string connStr = ConfigurationManager.ConnectionStrings["OPWContext"].ConnectionString;
                settings = db.GetUserSettings(connStr, strCurrentUserId);

                var output = await eBayItem.ListingCreateAsync(settings, itemId);
                if (ListingNotCreated(output))
                {
                    var errStr = dsutil.DSUtil.ListToDelimited(output.ToArray(), ';');
                    return BadRequest(errStr);
                }
                else
                {
                    var str = dsutil.DSUtil.ListToDelimited(output.ToArray(), ';');
                    return Ok(str);   // return listing id
                }
            }
            catch (Exception exc)
            {
                string msg = dsutil.DSUtil.ErrMsg("CreateListing", exc);
                dsutil.DSUtil.WriteFile(_logfile, msg, settings.UserName);
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
                Utility.eBayItem.EndFixedPriceItem(settings, item.ListedItemID);

                await db.UpdateRemovedDate(item);
                return Ok();
            }
            catch (Exception exc)
            {
                string msg = dsutil.DSUtil.ErrMsg("RemoveListing", exc);
                dsutil.DSUtil.WriteFile(_logfile, msg, settings.UserName);
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
                dsutil.DSUtil.WriteFile(_logfile, msg, userName);
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
                dsutil.DSUtil.WriteFile(_logfile, msg, userName);
                return BadRequest(msg);
            }
        }

        [HttpGet]
        [Route("getlistings")]
        public IHttpActionResult GetListings()
        {
            var settings = new UserSettingsView();
            try
            {
                string strCurrentUserId = User.Identity.GetUserId();
                string connStr = ConfigurationManager.ConnectionStrings["OPWContext"].ConnectionString;
                settings = db.GetUserSettings(connStr, strCurrentUserId);

                var listings = db.GetListings(settings.StoreID);
                if (listings == null)
                    return NotFound();
                return Ok(listings);
            }
            catch (Exception exc)
            {
                string msg = dsutil.DSUtil.ErrMsg("GetListings", exc);
                dsutil.DSUtil.WriteFile(_logfile, msg, settings.UserName);
                return BadRequest(msg);
            }
        }

        [HttpGet]
        [Route("getwmitem")]
        public async Task<IHttpActionResult> GetWMItem(string userName, string url)
        {
            try
            {
                var w = await wallib.wmUtility.GetDetail(url);
                return Ok(w);
            }
            catch (Exception exc)
            {
                string msg = dsutil.DSUtil.ErrMsg("GetWMItem", exc);
                dsutil.DSUtil.WriteFile(_logfile, msg, userName);
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
            var settings = new UserSettingsView();
            try
            {
                string strCurrentUserId = User.Identity.GetUserId();
                string connStr = ConfigurationManager.ConnectionStrings["OPWContext"].ConnectionString;
                settings = db.GetUserSettings(connStr, strCurrentUserId);

                await db.HistoryRemove(rptNumber);
                return Ok();
            }
            catch (Exception exc)
            {
                string msg = exc.Message;
                dsutil.DSUtil.WriteFile(_logfile, msg, settings.UserName);
                return Content(HttpStatusCode.InternalServerError, msg);
            }
        }

        [HttpDelete]
        [Route("deletelistingrecord/{sellerItemId}")]
        [AcceptVerbs("DELETE")]
        public async Task<IHttpActionResult> DeleteListingRecord(string sellerItemId)
        {
            var settings = new UserSettingsView();
            try
            {
                string strCurrentUserId = User.Identity.GetUserId();
                string connStr = ConfigurationManager.ConnectionStrings["OPWContext"].ConnectionString;
                settings = db.GetUserSettings(connStr, strCurrentUserId);

                await db.DeleteListingRecord(sellerItemId);
                return Ok();
            }
            catch (Exception exc)
            {
                string msg = exc.Message;
                dsutil.DSUtil.WriteFile(_logfile, msg, settings.UserName);
                return Content(HttpStatusCode.InternalServerError, msg);
            }
        }
        [HttpGet]
        [Route("dashboard")]
        public IHttpActionResult GetDashboard()
        {
            var settings = new UserSettingsView();
            try
            {
                string strCurrentUserId = User.Identity.GetUserId();
                string connStr = ConfigurationManager.ConnectionStrings["OPWContext"].ConnectionString;
                settings = db.GetUserSettings(connStr, strCurrentUserId);

                var dashboard = new Dashboard();
                int OOS = db.Listings.Where(p => p.OOS && p.StoreID == settings.StoreID).Count();
                dashboard.OOS = OOS;

                int notListed = db.Listings.Where(p => p.Listed == null && p.StoreID == settings.StoreID).Count();
                dashboard.NotListed = notListed;

                return Ok(dashboard);
            }
            catch (Exception exc)
            {
                string msg = dsutil.DSUtil.ErrMsg("GetDashboard", exc);
                dsutil.DSUtil.WriteFile(_logfile, msg, settings.UserName);
                return BadRequest(msg);
            }
        }
        [HttpGet]
        [Route("storeanalysis")]
        public IHttpActionResult StoreAnalysis()
        {
            var settings = new UserSettingsView();
            try
            {
                string strCurrentUserId = User.Identity.GetUserId();
                string connStr = ConfigurationManager.ConnectionStrings["OPWContext"].ConnectionString;
                settings = db.GetUserSettings(connStr, strCurrentUserId);

                var analysis = new StoreAnalysis();

                var storeItems = new ItemTypeCollection();
                analysis.DBIsMissingItems = StoreCheck.DBIsMissingItems(settings, ref storeItems);

                return Ok(analysis);
            }
            catch (Exception exc)
            {
                string msg = dsutil.DSUtil.ErrMsg("StoreAnalysis", exc);
                dsutil.DSUtil.WriteFile(_logfile, msg, settings.UserName);
                return BadRequest(msg);
            }
        }

        [HttpGet]
        [Route("walmartsearchprodid")]
        public IHttpActionResult WalmartSearchProdID(string search)
        {
            var settings = new UserSettingsView();
            try
            {
                var response = wallib.wmUtility.SearchProdID(search);

                return Ok(response);
            }
            catch (Exception exc)
            {
                string msg = dsutil.DSUtil.ErrMsg("MatchWalmart", exc);
                dsutil.DSUtil.WriteFile(_logfile, msg, settings.UserName);
                return BadRequest(msg);
            }
        }
    }
}