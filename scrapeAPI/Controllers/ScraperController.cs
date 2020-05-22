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
using System.Web;
using Microsoft.AspNet.Identity.Owin;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Web.Http.Results;
using dsmodels;
using Microsoft.AspNet.Identity;
using eBay.Service.Core.Soap;
using eBayUtility;
using System.Configuration;
using Utility;
using System.Data.Entity;
using System.IO;

namespace scrapeAPI.Controllers
{
    [Authorize]
    public class ScraperController : ApiController
    {
        dsmodels.DataModelsDB db = new dsmodels.DataModelsDB();
        Models.DataModelsDB models = new Models.DataModelsDB();

        const string _filename = "order.csv";
        const string _logfile = "log.txt";

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
                var sh = db.SearchHistoryView.AsNoTracking().OrderByDescending(x => x.Updated).ToList();
                return Ok(sh);
            }
            catch (Exception exc)
            {
                string msg = dsutil.DSUtil.ErrMsg("GetSearchHistory", exc);
                return Content(HttpStatusCode.InternalServerError, msg);
            }
        }

        /// <summary>
        /// Cancel running scan.
        /// </summary>
        /// <param name="rptNumber"></param>
        /// <returns></returns>
        [Route("cancelscan/{rptNumber}")]
        [HttpGet]
        public IHttpActionResult CancelScan(int rptNumber)
        {
            string strCurrentUserId = User.Identity.GetUserId();
            string connStr = ConfigurationManager.ConnectionStrings["OPWContext"].ConnectionString;
            //var settings = db.GetUserSettingsView(connStr, strCurrentUserId);

            try
            {
                var f = db.SearchHistory.Where(p => p.ID == rptNumber).FirstOrDefault();
                if (f != null)
                {
                    f.Running = false;
                    db.SearchHistoryUpdate(f, "Running");
                }
                return Ok();
            }
            catch (Exception exc)
            {
                string msg = dsutil.DSUtil.ErrMsg("CancelScan", exc);
                return Content(HttpStatusCode.InternalServerError, msg);
            }
        }

        /// <summary>
        /// Calculate new supplier price. 
        /// </summary>
        /// <param name="supplierPrice"></param>
        /// <returns></returns>
        [Route("calculatewmpx")]
        [HttpGet]
        public IHttpActionResult CalculateWMPrice(decimal supplierPrice, double pctProfit)
        {
            string strCurrentUserId = User.Identity.GetUserId();

            try
            {
                string connStr = ConfigurationManager.ConnectionStrings["OPWContext"].ConnectionString;
                var settings = db.GetUserSettingsView(connStr, strCurrentUserId);
                decimal wmShipping = Convert.ToDecimal(db.GetAppSetting(settings, "Walmart shipping"));
                decimal wmFreeShippingMin = Convert.ToDecimal(db.GetAppSetting(settings, "Walmart free shipping min"));
                var px = wallib.wmUtility.wmNewPrice(supplierPrice, pctProfit, wmShipping, wmFreeShippingMin, settings.FinalValueFeePct);
                return Ok(px);
            }
            catch (Exception exc)
            {
                string msg = dsutil.DSUtil.ErrMsg("CalculateWMPrice", exc);
                dsutil.DSUtil.WriteFile(_logfile, msg, strCurrentUserId);
                return Content(HttpStatusCode.InternalServerError, msg);
            }
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
        /// <param name="filter"></param>
        /// <returns></returns>
        [Route("getreport/{rptNumber}/{minSold}/{daysBack}/{minPrice}/{maxPrice}/{activeStatusOnly}/{isSellerVariation}/{itemID}/{filter}/{storeID}/{isSupplierVariation}/{priceDelta}/{excludeListed}/{excludeFreight}")]
        [HttpGet]
        public IHttpActionResult GetReport(int rptNumber, int minSold, int daysBack, int? minPrice, int? maxPrice, bool? activeStatusOnly, bool? isSellerVariation, string itemID, int filter, int storeID, bool? isSupplierVariation, bool? priceDelta, bool? excludeListed, bool? excludeFreight)
        {
            string strCurrentUserId = User.Identity.GetUserId();
            string connStr = ConfigurationManager.ConnectionStrings["OPWContext"].ConnectionString;
            var settings = db.GetUserSettingsView(connStr, strCurrentUserId);

            DateTime ModTimeTo = DateTime.Now.ToUniversalTime();
            DateTime ModTimeFrom = ModTimeTo.AddDays(-daysBack);

            try
            {
                itemID = (itemID == "null") ? null : itemID;

                IQueryable<TimesSold> x = null;
                x = db.GetSalesData(rptNumber, ModTimeFrom, storeID, itemID);
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
                    x = x.Where(p => p.IsSellerVariation == isSellerVariation.Value);
                }
                if (isSupplierVariation.HasValue)
                {
                    x = x.Where(p => p.IsSupplierVariation == isSupplierVariation.Value);
                }
                if (excludeListed.HasValue)
                {
                    x = x.Where(p => p.Listed == null);
                }
                if (excludeFreight.HasValue)
                {
                    //x = x.Where(p => p.IsFreightShipping == null || p.IsFreightShipping == false);
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
                        if (item.ProposePrice.HasValue)
                        {
                            var r = (item.EbaySellerPrice - item.ProposePrice.Value) / item.ProposePrice;
                            item.PriceDelta = r;
                        }
                    }
                }
                if (!priceDelta.HasValue)
                {
                    mv.TimesSoldRpt = mv.TimesSoldRpt.OrderByDescending(p => p.LastSold).ToList();
                }
                if (priceDelta.HasValue)
                {
                    decimal pxDelta = Convert.ToDecimal(db.GetAppSetting(settings, "priceDelta"));
                    mv.TimesSoldRpt = mv.TimesSoldRpt.Where(o => o.PriceDelta > pxDelta).OrderByDescending(p => p.LastSold).ToList();
                }
                mv.ListingsProcessed = 0;
                mv.TotalOrders = 0;
                mv.ItemCount = mv.TimesSoldRpt.Count;

                return Ok(mv);
            }
            catch (Exception exc)
            {
                string msg = dsutil.DSUtil.ErrMsg("GetReport", exc);
                dsutil.DSUtil.WriteFile(_logfile, msg, strCurrentUserId);
                return Content(HttpStatusCode.InternalServerError, msg);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="rptNumber">Pass rptNumber to run single seller</param>
        /// <param name="minSold"></param>
        /// <param name="daysBack"></param>
        /// <param name="minPrice"></param>
        /// <param name="maxPrice"></param>
        /// <param name="activeStatusOnly"></param>
        /// <param name="isSellerVariation"></param>
        /// <param name="itemID"></param>
        /// <param name="storeID">Pass store id to run all sellers in store</param>
        /// <returns>Need to determine what is best return value</returns>
        [Route("fillmatch/{rptNumber}/{minSold}/{daysBack}/{minPrice}/{maxPrice}/{activeStatusOnly}/{isSellerVariation}/{itemID}/{storeID}")]
        [HttpGet]
        public async Task<IHttpActionResult> FillMatch(int rptNumber, int minSold, int daysBack, int? minPrice, int? maxPrice, bool? activeStatusOnly, bool? isSellerVariation, string itemID, int storeID)
        {
            UserSettingsView settings = null;
            try
            {
                string strCurrentUserId = User.Identity.GetUserId();
                string connStr = ConfigurationManager.ConnectionStrings["OPWContext"].ConnectionString;
                settings = db.GetUserSettingsView(connStr, strCurrentUserId);
                decimal wmShipping = Convert.ToDecimal(db.GetAppSetting(settings, "Walmart shipping"));
                decimal wmFreeShippingMin = Convert.ToDecimal(db.GetAppSetting(settings, "Walmart free shipping min"));
                double pctProfit = settings.PctProfit;
                int imgLimit = Convert.ToInt32(db.GetAppSetting(settings, "Listing Image Limit"));

                string ret = await FetchSeller.CalculateMatch(settings, rptNumber, minSold, daysBack, minPrice, maxPrice, activeStatusOnly, isSellerVariation, itemID, pctProfit, 0, wmShipping, wmFreeShippingMin, settings.FinalValueFeePct, imgLimit, "walmart");
                if (string.IsNullOrEmpty(ret))
                {
                    return Ok();
                }
                else
                {
                    return BadRequest(ret);
                }
            }
            catch (Exception exc)
            {
                string msg = dsutil.DSUtil.ErrMsg("FillMatch", exc);
                dsutil.DSUtil.WriteFile(_logfile, msg, settings.UserName);
                return Content(HttpStatusCode.InternalServerError, msg);
            }
        }

        /// <summary>
        /// Does the given email exist.
        /// </summary>
        /// <param name="email"></param>
        /// <returns></returns>
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
                dsutil.DSUtil.WriteFile(_logfile, msg, "admin");
                return Content(HttpStatusCode.InternalServerError, msg);
            }
        }

        /// <summary>
        /// Is the username taken
        /// </summary>
        /// <param name="username"></param>
        /// <returns></returns>
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
                return Content(HttpStatusCode.InternalServerError, msg);
            }
        }

        /// <summary>
        /// How much of tradng API is used.
        /// </summary>
        /// <param name="userName"></param>
        /// <returns></returns>
        [HttpGet]
        [Route("tradingapiusage")]
        public async Task<IHttpActionResult> GetTradingAPIUsage(string userName)
        {
            try
            {
                var user = await UserManager.FindByNameAsync(userName);
                string connStr = ConfigurationManager.ConnectionStrings["OPWContext"].ConnectionString;
                var settings = db.GetUserSettingsView(connStr, user.Id);

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
                return Content(HttpStatusCode.InternalServerError, msg);
            }
        }

        /// <summary>
        /// Get status of token
        /// </summary>
        /// <param name="userName"></param>
        /// <returns></returns>
        [HttpGet]
        [Route("tokenstatustype")]
        public async Task<IHttpActionResult> GetTokenStatus(string userName, int storeID)
        {
            try
            {
                var user = await UserManager.FindByNameAsync(userName);
                string connStr = ConfigurationManager.ConnectionStrings["OPWContext"].ConnectionString;
                var settings = db.GetUserSettingsView(connStr, user.Id, storeID);

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
                return Content(HttpStatusCode.InternalServerError, msg);
            }
        }

        /// <summary>
        /// Return a seller listing.
        /// </summary>
        /// <param name="userName"></param>
        /// <param name="itemID"></param>
        /// <returns></returns>
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
                    var settings = db.GetUserSettingsView(connStr, user.Id);

                    var i = await ebayAPIs.GetSingleItem(settings, itemID, true);
                    return Ok(i);
                }
            }
            catch (Exception exc)
            {
                string msg = "GetSellerListing: " + exc.Message;
                dsutil.DSUtil.WriteFile(_logfile, msg, "nousername");
                return Content(HttpStatusCode.InternalServerError, msg);
            }
        }
        /// <summary>
        /// Move a staged listing to the Listing table.
        /// </summary>
        /// <param name="userName"></param>
        /// <param name="storeID"></param>
        /// <returns></returns>
        [HttpGet]
        [Route("storetolisting")]
        public async Task<IHttpActionResult> StoreToListing(string userName, int storeID)
        {
            /*
             * 01.14.2020 Seemingly out of the blue I started getting:
             * "Access to XMLHttpRequest at has been blocked by CORS policy"
             * search google and see things about enabling CORS.
             * But I just changed the param form passing from Angular to a querystring and now seems to work.
             */
            try
            {
                var user = await UserManager.FindByNameAsync(userName);
                if (user == null)
                    return Ok(false);
                else
                {
                    string connStr = ConfigurationManager.ConnectionStrings["OPWContext"].ConnectionString;
                    var settings = db.GetUserSettingsView(connStr, user.Id);
                    string ret = await eBayUtility.FetchSeller.StoreToListing(settings, storeID);
                    return Ok(ret);
                }
            }
            catch (Exception exc)
            {
                string msg = "StoreToListing: " + exc.Message;
                dsutil.DSUtil.WriteFile(_logfile, msg, "nousername");
                return Content(HttpStatusCode.InternalServerError, msg);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="listing"></param>
        /// <returns></returns>
        [HttpPost]
        [Route("listingsave")]
        public async Task<IHttpActionResult> ListingSave(ListingDTO dto, bool updateItemSpecifics)
        {
            string strCurrentUserId = User.Identity.GetUserId();
            try
            {
                string connStr = ConfigurationManager.ConnectionStrings["OPWContext"].ConnectionString;
                var settings = db.GetUserSettingsView(connStr, strCurrentUserId);

                // New listing
                if (dto.Listing.ID == 0)
                {
                    if (dto.Listing.SupplierItem == null)
                    {
                        string msg = "Could not read supplier item.";
                        return BadRequest(msg);
                    }
                    var ret = wallib.wmUtility.isValidProductURL(dto.Listing.SupplierItem.ItemURL);
                    if (!ret)
                    {
                        string msg = "Supplier URL is not of walmart pattern.";
                        return BadRequest(msg);
                    }
                    var si = db.GetSellerListing(dto.Listing.ItemID);
                    if (si == null)
                    {
                        var sellerListing = await ebayAPIs.GetSingleItem(settings, dto.Listing.ItemID, true);
                        if (sellerListing != null)
                        {
                            dto.Listing.SellerListing = sellerListing;
                            // if new listing and no title provided, then copy title from seller
                            if (string.IsNullOrEmpty(dto.Listing.ListingTitle))
                            {
                                dto.Listing.ListingTitle = sellerListing.Title;
                            }
                            sellerListing.Updated = DateTime.Now;
                            dto.Listing.PrimaryCategoryID = sellerListing.PrimaryCategoryID;
                            dto.Listing.PrimaryCategoryName = sellerListing.PrimaryCategoryName;
                            dto.Listing.eBaySellerURL = sellerListing.EbayURL;

                            // copy seller listing item specifics
                            var specifics = dsmodels.DataModelsDB.CopyItemSpecificFromSellerListing(dto.Listing, sellerListing.ItemSpecifics);
                            var revisedItemSpecs = eBayItem.ModifyItemSpecific(specifics);
                            dto.Listing.ItemSpecifics = revisedItemSpecs;
                        }
                        else
                        {
                            string msg = "ERROR: eBay seller item ID could not be found.";
                            return BadRequest(msg);
                        }
                    }
                    else
                    {
                        dto.Listing.ItemID = dto.Listing.ItemID;
                        dto.Listing.PrimaryCategoryID = si.PrimaryCategoryID;
                        dto.Listing.PrimaryCategoryName = si.PrimaryCategoryName;
                    }

                    // for new listing, supplier pulled but don't know if exists in db yet....
                    if (dto.Listing.SupplierItem.ID == 0)
                    {
                        // exists in db?
                        var r = db.GetSupplierItemByURL(dto.Listing.SupplierItem.ItemURL);
                        if (r != null)
                        {
                            dto.Listing.SupplierID = r.ID;
                            dto.Listing.SupplierItem = null;
                        }
                    }
                }
                else
                {
                    // Pull exiting record and see if ebay item id changed.
                    var listing = db.ListingGet(dto.Listing.ID);
                    if (listing != null)
                    {
                        if (!string.IsNullOrEmpty(listing.ItemID))
                        {
                            if (listing.ItemID != dto.Listing.ItemID)
                            {
                                var sellerListing = await ebayAPIs.GetSingleItem(settings, dto.Listing.ItemID, true);
                                if (sellerListing != null)
                                {
                                    dto.Listing.SellerListing = sellerListing;
                                    sellerListing.Updated = DateTime.Now;
                                    dto.Listing.PrimaryCategoryID = sellerListing.PrimaryCategoryID;
                                    dto.Listing.PrimaryCategoryName = sellerListing.PrimaryCategoryName;

                                    dto.Listing.ItemSpecifics = dsmodels.DataModelsDB.CopyItemSpecificFromSellerListing(dto.Listing, sellerListing.ItemSpecifics);
                                }
                                else
                                {
                                    string msg = "ERROR: eBay seller item ID could not be found.";
                                    return BadRequest(msg);
                                }
                            }
                        }
                    }
                }
                var updatedListing = await db.ListingSaveAsync(settings, dto.Listing, updateItemSpecifics, dto.FieldNames.ToArray());
                updatedListing.Warning = dsutil.DSUtil.GetDescrWarnings(updatedListing.Description);

                return Ok(updatedListing);
            }
            catch (Exception exc)
            {
                string msg = dsutil.DSUtil.ErrMsg("ListingSave", exc);
                dsutil.DSUtil.WriteFile(_logfile, msg, strCurrentUserId);
                return Content(HttpStatusCode.InternalServerError, msg);
            }
        }
        /// <summary>
        /// Store the user settings.
        /// </summary>
        /// <param name="dto"></param>
        /// <returns></returns>
        [HttpPost]
        [Route("usersettingssave")]
        public async Task<IHttpActionResult> UserSettingsSave(UserSettingsDTO dto)
        {
            UserSettingsView settings = null;
            try
            {
                string connStr = ConfigurationManager.ConnectionStrings["OPWContext"].ConnectionString;
                string strCurrentUserId = User.Identity.GetUserId();
                settings = db.GetUserSettingsView(connStr, strCurrentUserId);
                dto.UserSettings.UserID = strCurrentUserId;
                dto.UserSettings.ApplicationID = 1;
                await db.UserSettingsSaveAsync(dto.UserSettings, dto.FieldNames.ToArray());
                return Ok();
            }
            catch (Exception exc)
            {
                string msg = dsutil.DSUtil.ErrMsg("UserSettingsSave", exc);
                dsutil.DSUtil.WriteFile(_logfile, msg, settings.UserName);
                return BadRequest(msg);
            }
        }
        /// <summary>
        /// Store users' stage to listing selections.
        /// </summary>
        /// <param name="dto"></param>
        /// <returns></returns>
        [HttpPost]
        [Route("updatetolisting")]
        public async Task<IHttpActionResult> UpdateToListingSave(UpdateToListingDTO dto)
        {
            try
            {
                string strCurrentUserId = User.Identity.GetUserId();
                dto.UpdateToListing.UserID = strCurrentUserId;
                await db.UpdateToListingSave(dto.UpdateToListing, dto.FieldNames.ToArray());
                return Ok();
            }
            catch (Exception exc)
            {
                string msg = dsutil.DSUtil.ErrMsg("UpdateToListingSave", exc);
                dsutil.DSUtil.WriteFile(_logfile, msg, "nousername");
                return BadRequest(msg);
            }
        }

        /// <summary>
        /// Store a note that user puts on a listing.
        /// </summary>
        /// <param name="note"></param>
        /// <returns></returns>
        [HttpPost]
        [Route("storenote")]
        public async Task<IHttpActionResult> StoreNote(ListingNote note)
        {
            try
            {
                string strCurrentUserId = User.Identity.GetUserId();
                string connStr = ConfigurationManager.ConnectionStrings["OPWContext"].ConnectionString;
                //var settings = db.GetUserSettingsView(connStr, strCurrentUserId);

                note.UserID = strCurrentUserId;
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

        /// <summary>
        /// Get list of Listing notes.
        /// </summary>
        /// <param name="itemID"></param>
        /// <param name="storeID"></param>
        /// <returns></returns>
        [HttpGet]
        [Route("itemnotes")]
        public async Task<IHttpActionResult> GetItemNotes(string itemID, int storeID)
        {
            try
            {
                string strCurrentUserId = User.Identity.GetUserId();
                string connStr = ConfigurationManager.ConnectionStrings["OPWContext"].ConnectionString;
                var settings = db.GetUserSettingsView(connStr, strCurrentUserId);

                var notes = await db.ItemNotes(itemID, storeID);
                return Ok(notes);
            }
            catch (Exception exc)
            {
                string msg = dsutil.DSUtil.ErrMsg("GetItemNotes", exc);
                dsutil.DSUtil.WriteFile(_logfile, msg, "nousername");
                return BadRequest(msg);
            }
        }

        /// <summary>
        /// Store user note put on a seller.
        /// </summary>
        /// <param name="sellerProfile"></param>
        /// <returns></returns>
        [HttpPost]
        [Route("storesellerprofile")]
        public async Task<IHttpActionResult> StoreSellerProfile(SellerProfile sellerProfile)
        {
            try
            {
                string strCurrentUserId = User.Identity.GetUserId();
                sellerProfile.Updated = DateTime.Now;
                sellerProfile.UpdatedBy = strCurrentUserId;
                sellerProfile.UserID = strCurrentUserId;
                await db.SellerProfileSave(sellerProfile,
                    "Note",
                    "Updated",
                    "UpdatedBy");
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
        /// Create a listing on eBay
        /// </summary>
        /// <param name="itemID">ebay seller listing id</param>
        /// <returns></returns>
        [HttpGet]
        [Route("createlisting")]
        public async Task<IHttpActionResult> CreateListing(int listingID)
        {
            var settings = new UserSettingsView();
            try
            {
                string strCurrentUserId = User.Identity.GetUserId();
                string connStr = ConfigurationManager.ConnectionStrings["OPWContext"].ConnectionString;
                settings = db.GetUserSettingsView(connStr, strCurrentUserId);

                var output = await eBayItem.ListingCreateAsync(settings, listingID);
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

        /// <summary>
        /// BETA - create a variation listing.
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        [Route("createvariationlisting")]
        public async Task<IHttpActionResult> CreateVariationListing()
        {
            string output = null;
            var settings = new UserSettingsView();
            try
            {
                string strCurrentUserId = User.Identity.GetUserId();
                string connStr = ConfigurationManager.ConnectionStrings["OPWContext"].ConnectionString;
                settings = db.GetUserSettingsView(connStr, strCurrentUserId);

                // for testing, get some variation listing for item specifics
                // https://www.ebay.com/itm/Low-Profile-Microwave-Oven-RV-Dorm-Mini-Small-Best-Compact-Kitchen-Countertop-/133041437329?var=0
                //var sellerListing = await ebayAPIs.GetSingleItem(settings, "133041437329");
                //output = eBayItem.AddFPItemWithVariations_microwave(1, sellerListing);

                // https://www.ebay.com/itm/The-Pioneer-Woman-Cowboy-Rustic-Cutlery-14-Piece-Kitchen-Tools-Multiple-Colors/132929127680?epid=3021775004&hash=item1ef3318500:m:mxO3U5ZeMusEzRnLL58bpkw
                //var sellerListing = await ebayAPIs.GetSingleItem(settings, "132929127680");
                //output = eBayItem.AddFPItemWithVariations_cutlery(1, sellerListing);

                // https://www.ebay.com/itm/Rachel-Ray-Cookware-Set-Nonstick-Enamel-Marine-Blue-Non-Stick-Enamel-Pots-Pans/133245568450?hash=item1f060e05c2:m:miqH_90tUTRCqI2Zd3KZfTQ
                var sellerListing = await ebayAPIs.GetSingleItem(settings, "133245568450", true);
                output = eBayItemVariation.AddFPItemWithVariations_potspans(settings, 1, sellerListing);

                // eBayItem.ReviseFixedPriceItem("223892293783", "Color", "White");

                return Ok(output);   // return listing id
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

        /// <summary>
        /// End the listing on eBay and clean up db.
        /// </summary>
        /// <param name="listedID"></param>
        /// <returns></returns>
        [HttpGet]
        [Route("endlisting")]
        public async Task<IHttpActionResult> EndListing(int listingID)
        {
            var settings = new UserSettingsView();
            string strCurrentUserId = User.Identity.GetUserId();
            try
            {
                string connStr = ConfigurationManager.ConnectionStrings["OPWContext"].ConnectionString;
                settings = db.GetUserSettingsView(connStr, strCurrentUserId);
                var listing = db.ListingGet(listingID);

                /*
                bool salesExist = db.SalesExists(listing.ListedItemID);
                if (!salesExist)
                {
                    // can delete from db - what's the point of keeping track of what did not sell?
                    await db.DeleteListingRecordAsync(settings, listing.ID, true);
                }
                else
                {
                    listing.Listed = null;
                    listing.Ended = DateTime.Now;
                    listing.EndedBy = strCurrentUserId;
                    await db.ListingSaveAsync(settings, listing, "Ended", "EndedBy", "Listed");
                }
                string ret = Utility.eBayItem.EndFixedPriceItem(settings, listing);
                */

                // delist and leave in database
                bool auctionWasEnded;
                string ret = Utility.eBayItem.EndFixedPriceItem(settings, listing, out auctionWasEnded);
                if (auctionWasEnded)
                {
                    return BadRequest("Auction was ended");
                }
                listing.Listed = null;
                listing.Ended = DateTime.Now;
                listing.EndedBy = strCurrentUserId;
                await db.ListingSaveAsync(settings, listing, false, "Ended", "EndedBy", "Listed");

                var log = new ListingLog();
                log.MsgID = 900;
                log.Note = "listing, " + listing.ListedItemID + ", ended by " + settings.UserName;
                log.ListingID = listing.ID;
                log.UserID = settings.UserID;
                await db.ListingLogAdd(log);

                return Ok(ret);
            }
            catch (Exception exc)
            {
                string msg = dsutil.DSUtil.ErrMsg("EndListing", exc);
                dsutil.DSUtil.WriteFile(_logfile, msg, strCurrentUserId);
                return new ResponseMessageResult(Request.CreateErrorResponse(HttpStatusCode.InternalServerError, exc.Message));
            }
        }

        [HttpPost]
        [Route("setorder")]
        public IHttpActionResult SetOrder(Listing listing, DateTime fromDate, DateTime toDate)
        {
            var settings = new UserSettingsView();
            string strCurrentUserId = User.Identity.GetUserId();
            try
            {
                string connStr = ConfigurationManager.ConnectionStrings["OPWContext"].ConnectionString;
                settings = db.GetUserSettingsView(connStr, strCurrentUserId);

                var eBayOrders = ebayAPIs.GetOrdersByDate(settings, listing.ListedItemID, fromDate, toDate);
                if (eBayOrders.Count > 0)
                {
                    //foreach(var o in eBayOrders)
                    //{
                    //    o.Profit = eBayUtility.FetchSeller.CalcProfit(o);
                    //}
                    return Ok(eBayOrders);
                }
                else
                {
                    return NotFound();
                }
            }
            catch (Exception exc)
            {
                return new ResponseMessageResult(Request.CreateErrorResponse(HttpStatusCode.InternalServerError, exc.Message));
            }
        }
        [HttpGet]
        [Route("getorders")]
        public IHttpActionResult GetOrders(DateTime fromDate, DateTime toDate)
        {
            var settings = new UserSettingsView();
            string strCurrentUserId = User.Identity.GetUserId();
            try
            {
                string connStr = ConfigurationManager.ConnectionStrings["OPWContext"].ConnectionString;
                settings = db.GetUserSettingsView(connStr, strCurrentUserId);

                var eBayOrders = ebayAPIs.GetOrdersByDate(settings, fromDate, toDate);
                if (eBayOrders.Count > 0)
                {
                    return Ok(eBayOrders);
                }
                else
                {
                    return NotFound();
                }
            }
            catch (Exception exc)
            {
                return new ResponseMessageResult(Request.CreateErrorResponse(HttpStatusCode.InternalServerError, exc.Message));
            }
        }
        /// <summary>
        /// Get an order that was placed on walmart.
        /// </summary>
        /// <param name="orderURL"></param>
        /// <returns></returns>
        [HttpGet]
        [Route("getwmorder")]
        public async Task<IHttpActionResult> GetWMOrder(string orderURL)
        {
            try
            {
                await wallib.wmUtility.GetOrder(orderURL);
                return Ok();
            }
            catch (Exception exc)
            {
                return new ResponseMessageResult(Request.CreateErrorResponse(HttpStatusCode.InternalServerError, exc.Message));
            }
        }

        /// <summary>
        /// Get a listing from the db.
        /// </summary>
        /// <param name="listingID"></param>
        /// <returns></returns>
        [HttpGet]
        [Route("getlisting")]
        public IHttpActionResult GetListing(int listingID)
        {
            string username = null;
            try
            {
                string strCurrentUserId = User.Identity.GetUserId();
                string connStr = ConfigurationManager.ConnectionStrings["OPWContext"].ConnectionString;
                var settings = db.GetUserSettingsView(connStr, strCurrentUserId);
                username = settings.UserName;

                var listing = db.ListingGet(listingID);
                if (listing == null)
                {
                    return NotFound();
                }
                else
                {
                    if (!string.IsNullOrEmpty(listing.Description))
                    {
                        listing.Warning = dsutil.DSUtil.GetDescrWarnings(listing.Description);
                    }
                }
                return Ok(listing);
            }
            catch (Exception exc)
            {
                string msg = dsutil.DSUtil.ErrMsg("GetListing", exc);
                dsutil.DSUtil.WriteFile(_logfile, msg, username);
                return Content(HttpStatusCode.InternalServerError, msg);
            }
        }

        /// <summary>
        /// Get a supplier item detail
        /// </summary>
        /// <param name="ID"></param>
        /// <returns></returns>
        [HttpGet]
        [Route("getsupplieritem")]
        public IHttpActionResult GetSupplierItem(int ID)
        {
            string username = null;
            try
            {
                string strCurrentUserId = User.Identity.GetUserId();
                string connStr = ConfigurationManager.ConnectionStrings["OPWContext"].ConnectionString;
                var settings = db.GetUserSettingsView(connStr, strCurrentUserId);
                username = settings.UserName;

                var listing = db.SupplierItemGet(ID);
                if (listing == null)
                {
                    return NotFound();
                }
                return Ok(listing);
            }
            catch (Exception exc)
            {
                string msg = dsutil.DSUtil.ErrMsg("GetSupplierItem", exc);
                dsutil.DSUtil.WriteFile(_logfile, msg, username);
                return Content(HttpStatusCode.InternalServerError, msg);
            }
        }

        /// <summary>
        /// Get a seller's profile.
        /// </summary>
        /// <param name="userName"></param>
        /// <param name="seller"></param>
        /// <returns></returns>
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
                return Content(HttpStatusCode.InternalServerError, msg);
            }
        }

        /// <summary>
        /// Get set of listings.
        /// </summary>
        /// <param name="storeID"></param>
        /// <param name="unlisted">only filter if set to True</param>
        /// <param name="listed">only filter if set to True</param>
        /// <returns></returns>
        [HttpGet]
        [Route("getlistings/{storeID}/{unlisted}/{listed}")]
        public IHttpActionResult GetListings(int storeID, bool unlisted, bool listed)
        {

            // eBayUtility.ebayAPIs.GetOrders("24-04242-80495", 1);
            var settings = new UserSettingsView();
            try
            {
                string strCurrentUserId = User.Identity.GetUserId();
                string connStr = ConfigurationManager.ConnectionStrings["OPWContext"].ConnectionString;
                settings = db.GetUserSettingsView(connStr, strCurrentUserId);

                // In both calls here, referring to RCA home theatre sold on 12/4/2019
                //eBayUtility.ebayAPIs.GetOrders("24-04242-80495", 1);
                // eBayUtility.ebayAPIs.ProcessTransactions(settings, "223707436249", new DateTime(2019, 12, 1), new DateTime(2019, 12, 15));

                var listings = db.GetListings(storeID, unlisted, listed);
                //var listings = db.GetListings(storeID, unlisted, listed);
                if (listings == null)
                    return NotFound();
                return Ok(listings.ToList());
            }
            catch (Exception exc)
            {
                string msg = dsutil.DSUtil.ErrMsg("GetListings", exc);
                dsutil.DSUtil.WriteFile(_logfile, msg, settings.UserName);
                return Content(HttpStatusCode.InternalServerError, msg);
            }
        }

        /// <summary>
        /// Get item from walmart website and determine validity.
        /// </summary>
        /// <param name="userName"></param>
        /// <param name="URL"></param>
        /// <returns></returns>
        [HttpGet]
        [Route("getwmitem")]
        public async Task<IHttpActionResult> GetWMItem(string userName, string URL)
        {
            try
            {
                var ret = wallib.wmUtility.isValidProductURL(URL);
                if (!ret)
                {
                    return BadRequest("Invalid Walmart URL.");  // throws error on client
                }
                string strCurrentUserId = User.Identity.GetUserId();
                string connStr = ConfigurationManager.ConnectionStrings["OPWContext"].ConnectionString;
                var settings = db.GetUserSettingsView(connStr, strCurrentUserId);

                byte handlingTime = settings.HandlingTime;
                byte maxShippingDays = settings.MaxShippingDays;
                var allowedDeliveryDays = handlingTime + maxShippingDays;
                int imgLimit = Convert.ToInt32(db.GetAppSetting(settings, "Listing Image Limit"));

                var w = await wallib.wmUtility.GetDetail(URL, imgLimit, false);
                if (w == null)
                {
                    string msg = "Could not fetch supplier item - possibly bad URL";
                    dsutil.DSUtil.WriteFile(_logfile, msg + " " + URL, userName);
                    return BadRequest(msg);
                }
                wallib.wmUtility.CanList(w, allowedDeliveryDays);
                return Ok(w);
            }
            catch (Exception exc)
            {
                string msg = dsutil.DSUtil.ErrMsg("GetWMItem", exc);
                dsutil.DSUtil.WriteFile(_logfile, msg, userName);
                return Content(HttpStatusCode.InternalServerError, msg);
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
                return Content(HttpStatusCode.InternalServerError, msg);
            }
        }

        /// <summary>
        /// Delete a seller scan.
        /// </summary>
        /// <param name="rptNumber"></param>
        /// <returns></returns>
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
                settings = db.GetUserSettingsView(connStr, strCurrentUserId);

                /*
                 * 
                 * Occassionally delete scan fails if itemID still in UpdateToListing.
                 * We do cleanup UpdateToListing so not sure why some records don't get removed.
                 * 
                 */
                await db.HistoryRemove(connStr, rptNumber);
                return Ok();
            }
            catch (Exception exc)
            {
                string msg = exc.Message;
                dsutil.DSUtil.WriteFile(_logfile, msg, settings.UserName);
                return Content(HttpStatusCode.InternalServerError, msg);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="listingID"></param>
        /// <returns></returns>
        [HttpDelete]
        [Route("deletelistingrecord/{listingID}")]
        [AcceptVerbs("DELETE")]
        public async Task<IHttpActionResult> DeleteListingRecord(int listingID)
        {
            var settings = new UserSettingsView();
            try
            {
                string strCurrentUserId = User.Identity.GetUserId();
                string connStr = ConfigurationManager.ConnectionStrings["OPWContext"].ConnectionString;
                settings = db.GetUserSettingsView(connStr, strCurrentUserId);

                string ret = await db.DeleteListingRecordAsync(settings, listingID, false);
                if (!string.IsNullOrEmpty(ret))
                {
                    return BadRequest(ret);
                }
                return Ok();
            }
            catch (Exception exc)
            {
                string msg = exc.Message;
                dsutil.DSUtil.WriteFile(_logfile, msg, settings.UserName);
                return Content(HttpStatusCode.InternalServerError, msg);
            }
        }

        /// <summary>
        /// Return data structure of dasbhoard figures.
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        [Route("dashboard")]
        public IHttpActionResult GetDashboard(int storeID)
        {
            var settings = new UserSettingsView();
            try
            {
                string strCurrentUserId = User.Identity.GetUserId();
                string connStr = ConfigurationManager.ConnectionStrings["OPWContext"].ConnectionString;
                settings = db.GetUserSettingsView(connStr, strCurrentUserId);
                //var profile = db.GetUserProfile(strCurrentUserId);

                // TESTING
                //eBayUtility.ebayAPIs.GetebayDetails(settings);
                //await eBayUtility.ebayAPIs.GetShippingPolicy(settings);
                //eBayItem.GetSellerBusinessPolicy(settings);

                if (settings != null)
                {
                    var dashboard = new Dashboard();
                    int OOS = db.Listings.Where(p => p.Qty == 0 && p.StoreID == storeID && p.Listed != null).Count();
                    dashboard.OOS = OOS;

                    int notListed = db.Listings.Where(p => p.Listed == null && p.StoreID == storeID).Count();
                    dashboard.NotListed = notListed;

                    int listed = db.Listings.Where(p => p.Listed != null && p.StoreID == storeID).Count();
                    dashboard.Listed = listed;

                    /*
                     * 04.10.2020 don't run this yet
                     * 
                    var storeItems = new ItemTypeCollection();
                    var dbMissingItems = StoreCheck.DBIsMissingItems(settings, ref storeItems);
                    */

                    return Ok(dashboard);
                }
                else
                {
                    return NotFound();
                }
            }
            catch (Exception exc)
            {
                string msg = dsutil.DSUtil.ErrMsg("GetDashboard", exc);
                dsutil.DSUtil.WriteFile(_logfile, msg, settings.UserName);
                return Content(HttpStatusCode.InternalServerError, msg);
            }
        }

        /// <summary>
        /// Determine properties of eBay store such as out of stock items that are now available.
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        [Route("storeanalysis")]
        public IHttpActionResult StoreAnalysis(int storeID)
        {
            var settings = new UserSettingsView();
            try
            {
                string strCurrentUserId = User.Identity.GetUserId();
                string connStr = ConfigurationManager.ConnectionStrings["OPWContext"].ConnectionString;
                settings = db.GetUserSettingsView(connStr, strCurrentUserId, storeID);


                var storeItems = new ItemTypeCollection();
                var analysis = StoreCheck.Analysis(settings, ref storeItems);
                //analysis.DBIsMissingItems = StoreCheck.DBIsMissingItems(settings, ref storeItems);

                return Ok(analysis);
            }
            catch (Exception exc)
            {
                string msg = dsutil.DSUtil.ErrMsg("StoreAnalysis", exc);
                dsutil.DSUtil.WriteFile(_logfile, msg, settings.UserName);
                return Content(HttpStatusCode.InternalServerError, msg);
            }
        }

        /// <summary>
        /// Look up UPC on walmart's website.
        /// </summary>
        /// <param name="search"></param>
        /// <returns></returns>
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
                return Content(HttpStatusCode.InternalServerError, msg);
            }
        }

        /// <summary>
        /// Count how many errors in log.
        /// </summary>
        /// <param name="filename"></param>
        /// <returns></returns>
        [HttpGet]
        [Route("logerrorcount")]
        public IHttpActionResult GetLogErrorCount(string filename)
        {
            try
            {
                string path = AppDomain.CurrentDomain.BaseDirectory;
                string fullpath = path + filename;
                int match = dsutil.DSUtil.FindError(fullpath, "ERROR");
                return Ok(match);
            }
            catch (Exception exc)
            {
                string msg = dsutil.DSUtil.ErrMsg("GetLogErrorCount", exc);
                return Content(HttpStatusCode.InternalServerError, msg);
            }
        }

        /// <summary>
        /// Return last error.
        /// </summary>
        /// <param name="filename"></param>
        /// <returns></returns>
        [HttpGet]
        [Route("lasterror")]
        public IHttpActionResult GetLastError(string filename)
        {
            try
            {
                string path = AppDomain.CurrentDomain.BaseDirectory;
                string fullpath = path + filename;
                string lastErr = dsutil.DSUtil.GetLastError(fullpath, "ERROR");
                return Ok(lastErr);
            }
            catch (Exception exc)
            {
                string msg = dsutil.DSUtil.ErrMsg("GetLastError", exc);
                return Content(HttpStatusCode.InternalServerError, msg);
            }
        }

        /// <summary>
        /// User may have multiple stores configured.
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        [Route("getuserstores")]
        public IHttpActionResult GetUserStores()
        {
            string strCurrentUserId = null;
            try
            {
                strCurrentUserId = User.Identity.GetUserId();
                string connStr = ConfigurationManager.ConnectionStrings["OPWContext"].ConnectionString;
               
                var userStores = db.GetUserStores(strCurrentUserId);
                if (userStores == null)
                {
                    return NotFound();
                }
                return Ok(userStores);
            }
            catch (Exception exc)
            {
                string msg = dsutil.DSUtil.ErrMsg("GetUserStores", exc);
                dsutil.DSUtil.WriteFile(_logfile, msg, strCurrentUserId);
                return Content(HttpStatusCode.InternalServerError, msg);
            }
        }

        /// <summary>
        /// BETA - store an order placed with the supplier for an eBay customer
        /// </summary>
        /// <param name="dto"></param>
        /// <returns></returns>
        [HttpPost]
        [Route("salesorderupdate")]
        public async Task<IHttpActionResult> SalesOrderSave(SalesOrderDTO dto)
        {
            UserSettingsView settings = null;
            try
            {
                string strCurrentUserId = User.Identity.GetUserId();
                string connStr = ConfigurationManager.ConnectionStrings["OPWContext"].ConnectionString;
                settings = db.GetUserSettingsView(connStr, strCurrentUserId);
                var exists = db.SalesOrderExists(dto.SalesOrder.SupplierOrderNumber);
                if (!exists)
                {
                    string msg = null;

                    var eBayOrder = eBayUtility.ebayAPIs.GetOrders(settings, dto.SalesOrder.eBayOrderNumber, out msg);

                    if (!string.IsNullOrEmpty(msg))
                    {
                        return Content(HttpStatusCode.InternalServerError, msg);
                    }
                    dto.SalesOrder.BuyerHandle = eBayOrder.BuyerHandle;
                    dto.SalesOrder.Buyer = eBayOrder.Buyer;
                    dto.SalesOrder.DatePurchased = eBayOrder.DatePurchased;
                    dto.SalesOrder.BuyerPaid = eBayOrder.BuyerPaid;
                    dto.SalesOrder.BuyerState = eBayOrder.BuyerState;
                    dto.FieldNames.Add("CustomerName");
                    dto.FieldNames.Add("CustomerHandle");
                    dto.FieldNames.Add("DatePurchased");
                    dto.FieldNames.Add("BuyerPaid");
                    dto.FieldNames.Add("BuyerState");
                }
                await db.SalesOrderSaveAsync(settings, dto.SalesOrder, dto.FieldNames.ToArray());
                return Ok();
            }
            catch (Exception exc)
            {
                string msg = dsutil.DSUtil.ErrMsg("SalesOrderSave", exc);
                dsutil.DSUtil.WriteFile(_logfile, msg, settings.UserName);
                return BadRequest(msg);
            }
        }
        /// <summary>
        /// 02.09.2020
        /// eBay will be requiring more item specifics
        /// </summary>
        /// <param name="ID">ID of listing</param>
        /// <returns></returns>
        [HttpGet]
        [Route("refreshitemspecifics")]
        public async Task<IHttpActionResult> RefreshItemSpecifics(int ID)
        {
            var settings = new UserSettingsView();
            try
            {
                string strCurrentUserId = User.Identity.GetUserId();
                string connStr = ConfigurationManager.ConnectionStrings["OPWContext"].ConnectionString;
                settings = db.GetUserSettingsView(connStr, strCurrentUserId);

                await eBayItem.RefreshItemSpecifics(settings, ID);
                return Ok();
            }
            catch (Exception exc)
            {
                string msg = dsutil.DSUtil.ErrMsg("RefreshItemSpecifics", exc);
                dsutil.DSUtil.WriteFile(_logfile, msg, settings.UserName);
                return Content(HttpStatusCode.InternalServerError, msg);
            }
        }

        [HttpGet]
        [Route("isvalidwalmarturl")]
        public IHttpActionResult IsValidWalmartURL(string URL)
        {
            try
            {
                var ret = wallib.wmUtility.isValidProductURL(URL);
                return Ok(ret);
            }
            catch (Exception exc)
            {
                var settings = new UserSettingsView();
                string strCurrentUserId = User.Identity.GetUserId();
                string connStr = ConfigurationManager.ConnectionStrings["OPWContext"].ConnectionString;
                settings = db.GetUserSettingsView(connStr, strCurrentUserId);

                string msg = dsutil.DSUtil.ErrMsg("IsValidWalmartURL", exc);
                dsutil.DSUtil.WriteFile(_logfile, msg, settings.UserName);
                return Content(HttpStatusCode.InternalServerError, msg);
            }
        }

        [HttpGet]
        [Route("getbusinesspolicies")]
        public IHttpActionResult GetBusinessPolicies(int storeID)
        {
            var settings = new UserSettingsView();
            try
            {
                string strCurrentUserId = User.Identity.GetUserId();
                string connStr = ConfigurationManager.ConnectionStrings["OPWContext"].ConnectionString;

                // need store Token
                settings = db.GetUserSettingsView(connStr, strCurrentUserId, storeID);
                if (settings != null)
                {
                    var policies = eBayItem.GetSellerBusinessPolicy(settings);
                    return Ok(policies);
                }
                else
                {
                    // new user, no stores configures yet
                    return NotFound();
                }
            }
            catch (Exception exc)
            {
                string msg = dsutil.DSUtil.ErrMsg("GetBusinessPolicies", exc);
                dsutil.DSUtil.WriteFile(_logfile, msg, settings.UserName);
                return Content(HttpStatusCode.InternalServerError, msg);
            }
        }
        [HttpGet]
        [Route("getlistinglog")]
        public IHttpActionResult GetListingLog(int listingID)
        {
            var settings = new UserSettingsView();
            try
            {
                string strCurrentUserId = User.Identity.GetUserId();
                string connStr = ConfigurationManager.ConnectionStrings["OPWContext"].ConnectionString;
                settings = db.GetUserSettingsView(connStr, strCurrentUserId);

                var log = db.ListingLogGet(listingID);
                return Ok(log);
            }
            catch (Exception exc)
            {
                string msg = dsutil.DSUtil.ErrMsg("GetListingLog", exc);
                dsutil.DSUtil.WriteFile(_logfile, msg, settings.UserName);
                return Content(HttpStatusCode.InternalServerError, msg);
            }
        }
        [HttpPost]
        [Route("listinglogadd")]
        public async Task<IHttpActionResult> ListingLogAdd(ListingLog log)
        {

            UserSettingsView settings = null;
            try
            {
                string strCurrentUserId = User.Identity.GetUserId();
                string connStr = ConfigurationManager.ConnectionStrings["OPWContext"].ConnectionString;
                settings = db.GetUserSettingsView(connStr, strCurrentUserId);

                await db.ListingLogAdd(log);
                return Ok();
            }
            catch (Exception exc)
            {
                string msg = dsutil.DSUtil.ErrMsg("ListingLogAdd", exc);
                dsutil.DSUtil.WriteFile(_logfile, msg, settings.UserName);
                return Content(HttpStatusCode.InternalServerError, msg);
            }
        }
        /// <summary>
        /// Sets up initial API keys
        /// </summary>
        /// <param name="dto"></param>
        /// <returns></returns>
        [HttpPost]
        [Route("ebaykeysupdate")]
        public async Task<IHttpActionResult> eBayKeysSave(eBayKeysDTO dto)
        {
            string strCurrentUserId = null;
            int storeID = dto.StoreID;
            try
            {
                strCurrentUserId = User.Identity.GetUserId();

                using (DbContextTransaction transaction = db.Database.BeginTransaction())
                {
                    try
                    {
                        StoreProfile profile;
                        // if we didn't get passed the ids then try to create the store in StoreProfile
                        if (dto.StoreID == 0 && dto.eBayKeys.ID == 0)
                        {
                            // try to add to StoreProfile
                            var user = Utility.eBayItem.GetUser(dto.Token);
                            var store = Utility.eBayItem.GetStore(dto.Token);
                            profile = new StoreProfile();
                            profile.eBayUserID = user.eBayUserID;
                            if (store != null)
                            {
                                // if user has paid for eBay store
                                profile.StoreName = store.StoreName;
                            }
                            await db.StoreProfileAddAsync(profile);
                            storeID = profile.ID;
                        }

                        // update eBayKeys
                        var keys = await db.UserProfileKeysUpdate(dto.eBayKeys, dto.FieldNames.ToArray());

                        // update UserToken
                        var ut = new UserToken
                        {
                            UserID = strCurrentUserId,
                            StoreID = storeID,
                            KeysID = keys.ID,
                            Token = dto.Token
                        };
                        await db.UserTokenUpdate(ut, "Token");

                        // Add to UserStore
                        var userStore = new UserStore
                        {
                            StoreID = storeID,
                            UserID = strCurrentUserId
                        };
                        await db.UserStoreAddAsync(userStore);

                        // add to UserSettings
                        /*
                        var settings = new UserSettings();
                        settings.UserID = strCurrentUserId;
                        settings.StoreID = storeID;
                        settings.KeysID = keys.ID;
                        settings.ApplicationID = 1;
                        await db.UserSettingsSaveAsync(settings);
                        */

                        transaction.Commit();
                    }
                    catch (Exception ex)
                    {
                        transaction.Rollback();
                        string msg = dsutil.DSUtil.ErrMsg("eBayKeysSave", ex);
                        dsutil.DSUtil.WriteFile(_logfile, msg, strCurrentUserId);
                        return Content(HttpStatusCode.InternalServerError, msg);
                    }
                }
                      
                return Ok();
            }
            catch (Exception exc)
            {
                string msg = dsutil.DSUtil.ErrMsg("eBayKeysSave", exc);
                dsutil.DSUtil.WriteFile(_logfile, msg, strCurrentUserId);
                return Content(HttpStatusCode.InternalServerError, msg);
            }
        }
        [HttpPost]
        [Route("storeprofileadd")]
        public async Task<IHttpActionResult> StoreAdd(StoreProfile profile)
        {
            string strCurrentUserId = string.Empty;
            try
            {
                strCurrentUserId = User.Identity.GetUserId();
                await db.StoreAddAsync(strCurrentUserId, profile);
                return Ok();
            }
            catch (Exception exc)
            {
                string msg = dsutil.DSUtil.ErrMsg("storeprofileadd", exc);
                dsutil.DSUtil.WriteFile(_logfile, msg, strCurrentUserId);
                return Content(HttpStatusCode.InternalServerError, msg);
            }
        }
   
        /// <summary>
        /// 
        /// </summary>
        /// <param name="storeID"></param>
        /// <returns>type eBayStore</returns>
        [HttpGet]
        [Route("getstore")]
        public IHttpActionResult GetStore(int storeID)
        {
            string strCurrentUserId = null;
            try
            {
                strCurrentUserId = User.Identity.GetUserId();
                string token = db.GetToken(storeID, strCurrentUserId);
                var u = Utility.eBayItem.GetUser(token);
                var eBayStore = Utility.eBayItem.GetStore(token);
                return Ok(eBayStore);
            }
            catch (Exception exc)
            {
                string msg = dsutil.DSUtil.ErrMsg("GetStore", exc);
                dsutil.DSUtil.WriteFile(_logfile, msg, strCurrentUserId);
                return Content(HttpStatusCode.InternalServerError, msg);
            }
        }
        [HttpGet]
        [Route("getuserprofilekeys")]
        public IHttpActionResult GetUserProfileKeys(int storeID)
        {
            string strCurrentUserId = null;
            try
            {
                strCurrentUserId = User.Identity.GetUserId();
                var keys = db.GetUserProfileKeysView(storeID, strCurrentUserId);
                return Ok(keys);
            }
            catch (Exception exc)
            {
                string msg = dsutil.DSUtil.ErrMsg("GetUserProfileKeys", exc);
                dsutil.DSUtil.WriteFile(_logfile, msg, strCurrentUserId);
                return Content(HttpStatusCode.InternalServerError, msg);
            }
        }
        [HttpGet]
        [Route("getebayuser")]
        public IHttpActionResult GeteBayUser(int storeID)
        {
            string strCurrentUserId = null;
            try
            {
                strCurrentUserId = User.Identity.GetUserId();
                var u = Utility.eBayItem.GeteBayUser(storeID, strCurrentUserId);
                return Ok(u);
            }
            catch (Exception exc)
            {
                string msg = dsutil.DSUtil.ErrMsg("GetUser", exc);
                dsutil.DSUtil.WriteFile(_logfile, msg, strCurrentUserId);
                return Content(HttpStatusCode.InternalServerError, msg);
            }
        }
        [HttpPost]
        [Route("salesorderadd")]
        public async Task<IHttpActionResult> SalesOrderAdd(SalesOrder salesOrder)
        {
            string strCurrentUserId = string.Empty;
            try
            {
                strCurrentUserId = User.Identity.GetUserId();
                salesOrder.CreatedBy = strCurrentUserId;
                salesOrder.Profit = eBayUtility.FetchSeller.CalcProfitOnSalesOrder(salesOrder);
                salesOrder.ProfitMargin = FetchSeller.CalcProfitMarginOnSalesOrder(salesOrder);
                var ret = await db.SalesOrderAddAsync(salesOrder);
                return Ok(ret);
            }
            catch (Exception exc)
            {
                string msg = dsutil.DSUtil.ErrMsg("SalesOrderAdd listingID", exc);
                dsutil.DSUtil.WriteFile(_logfile, msg, strCurrentUserId);
                return Content(HttpStatusCode.InternalServerError, msg);
            }
        }
       
    }
}