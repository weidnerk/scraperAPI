using eBay.Service.Call;
using eBay.Service.Core.Sdk;
using eBay.Service.Core.Soap;
using scrapeAPI.com.ebay.developer;
using scrapeAPI.com.ebay.developer1;
using System;
using System.Configuration;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using scrapeAPI.Models;
using System.Collections.Generic;
using scrapeAPI.Controllers;
using System.Xml.Serialization;
using System.IO;
using System.Xml.Linq;
using eBay.Service.Util;
using dsmodels;
using Microsoft.AspNet.Identity;

namespace scrapeAPI
{
    // TokenStatusType is an eBay type that has a property called Status which is an enum
    // Here, another property has been added to show the name of the enum
    public class TokenStatusTypeCustom : TokenStatusType
    {
        public string StatusStr { get; set; }
    }

    public class ebayAPIs
    {
        readonly static string _logfile = "scrape_log.txt";
        dsmodels.DataModelsDB db = new dsmodels.DataModelsDB();
        Models.DataModelsDB models = new Models.DataModelsDB();

        public static void EndFixedPriceItem(string itemID)
        {

            //create the context
            ApiContext context = new ApiContext();

            //set the User token
            string token = AppSettingsHelper.Token;
            context.ApiCredential.eBayToken = token;

            //set the server url
            string endpoint = AppSettingsHelper.Endpoint;
            context.SoapApiServerUrl = endpoint;

            //enable logging
            context.ApiLogManager = new ApiLogManager();
            context.ApiLogManager.ApiLoggerList.Add(new FileLogger("endprice_log.txt", true, true, true));
            context.ApiLogManager.EnableLogging = true;

            //set the version
            context.Version = "817";
            context.Site = eBay.Service.Core.Soap.SiteCodeType.US;

            EndFixedPriceItemCall endFP = new EndFixedPriceItemCall(context);

            endFP.ItemID = itemID;
            endFP.EndingReason = EndReasonCodeType.NotAvailable;

            endFP.Execute();
            Console.WriteLine(endFP.ApiResponse.Ack + " Ended ItemID " + endFP.ItemID);
        }


        // use this for itemspecifics:
        // https://ebaydts.com/eBayKBDetails?KBid=1647
        //
        public static string ReviseItem(string listedItemID, int? qty = null, double? price = null, string title = null)
        {
            //create the context
            ApiContext context = new ApiContext();

            //set the User token
            string token = AppSettingsHelper.Token;
            context.ApiCredential.eBayToken = token;

            //set the server url
            string endpoint = AppSettingsHelper.Endpoint;
            context.SoapApiServerUrl = endpoint;

            //enable logging
            context.ApiLogManager = new ApiLogManager();
            context.ApiLogManager.ApiLoggerList.Add(new FileLogger("ebay_log.txt", true, true, true));
            context.ApiLogManager.EnableLogging = true;

            //set the version
            context.Version = "817";
            context.Site = eBay.Service.Core.Soap.SiteCodeType.US;

            ReviseFixedPriceItemCall reviseFP = new ReviseFixedPriceItemCall(context);

            ItemType item = new ItemType();
            item.ItemID = listedItemID;

            if (qty.HasValue)
                item.Quantity = qty.Value;

            if (price.HasValue)
            {
                item.StartPrice = new eBay.Service.Core.Soap.AmountType
                {
                    Value = price.Value,
                    currencyID = eBay.Service.Core.Soap.CurrencyCodeType.USD
                };
            }

            if (!string.IsNullOrEmpty(title))
            {
                item.Title = title;
            }
            /*
            item.ItemSpecifics = new NameValueListTypeCollection();

            NameValueListTypeCollection ItemSpecs = new NameValueListTypeCollection();

            var nv1 = new eBay.Service.Core.Soap.NameValueListType();
            var nv2 = new eBay.Service.Core.Soap.NameValueListType();

            StringCollection valueCol1 = new StringCollection();
            StringCollection valueCol2 = new StringCollection();

            nv1.Name = "Brand";
            valueCol1.Add("Unbranded");
            nv1.Value = valueCol1;

            nv2.Name = "MPN";
            valueCol2.Add("Does Not Apply");
            nv2.Value = valueCol2;

            ItemSpecs.Add(nv1);
            ItemSpecs.Add(nv2);
            item.ItemSpecifics = ItemSpecs;

            var pd = new ProductListingDetailsType();
            //var brand = new BrandMPNType();
            //brand.Brand = "Unbranded";
            //brand.MPN = unavailable;
            //pd.BrandMPN = brand;
            pd.UPC = "Does not apply";
            item.ProductListingDetails = pd;
            */

            reviseFP.Item = item;

            reviseFP.Execute();
            var r = reviseFP.ApiResponse;
            string msg = r.Ack.ToString();
            if (r.Errors.Count > 0)
            {
                foreach (eBay.Service.Core.Soap.ErrorType e in r.Errors)
                {
                    msg += " " + e.LongMessage;
                }
            }
            return msg;
        }

        public static string GetebayDetails()
        {
            string unavailable = null;
            try
            {
                ApiContext context = new ApiContext();
                string token = AppSettingsHelper.Token;
                context.ApiCredential.eBayToken = token;
                context.SoapApiServerUrl = "https://api.ebay.com/wsapi";
                // set the version 
                context.Version = "865";
                context.Site = eBay.Service.Core.Soap.SiteCodeType.US;

                GeteBayDetailsCall request = new GeteBayDetailsCall(context);
                request.DetailNameList = new DetailNameCodeTypeCollection();
                request.DetailNameList.Add(DetailNameCodeType.ProductDetails);
                GeteBayDetailsResponseType response = new GeteBayDetailsResponseType();
                request.Execute();
                response = request.ApiResponse;
                if (response.Ack == eBay.Service.Core.Soap.AckCodeType.Success)
                {
                    unavailable = response.ProductDetails.ProductIdentifierUnavailableText;
                }
            }
            catch (Exception ex)
            {
                throw ex;
            }
            return unavailable;
        }

        // findCompletedItems
        // this is a member of the Finding API.  My understanding is that the .NET SDK only supports the Trading API
        public static async Task FindItemsAsync()
        {

            string uri = "http://svcs.ebay.com/services/search/FindingService/v1?SECURITY-APPNAME=KevinWei-Test-PRD-25d7a0307-a9330e4a&OPERATION-NAME=findCompletedItems&SERVICE-VERSION=1.13.0&GLOBAL-ID=EBAY-US&RESPONSE-DATA-FORMAT=JSON&REST-PAYLOAD&itemFilter(0).name=Seller&itemFilter(0).paramName=name&itemFilter(0).paramValue=Seller&itemFilter(0).value(0)=**justforyou**&itemFilter(1).name=SoldItemsOnly&itemFilter(1).value(0)=true";

            // ... Use HttpClient.
            using (HttpClient client = new HttpClient())
            using (HttpResponseMessage response = await client.GetAsync(uri))
            using (HttpContent content = response.Content)
            {
                // ... Read the string.
                string result = await content.ReadAsStringAsync();

                // ... Display the result.
                if (result != null &&
                    result.Length >= 50)
                {
                    Console.WriteLine(result);
                }
            }
        }

        // https://ebaydts.com/eBayKBDetails?KBid=475
        //
        // a variety of this is to use findCompletedItems
        //
        // I don't know how to filter this by completed items
        public static ItemTypeCollection GetSellerList(string seller, out string errMsg)
        {
            // TODO: Add code to start application here
            //
            // first, set up the ApiContext object
            errMsg = null;
            ApiContext oContext = new ApiContext();

            // set the dev,app,cert information
            oContext.ApiCredential.ApiAccount.Application = ConfigurationManager.AppSettings["AppID"];
            oContext.ApiCredential.ApiAccount.Developer = ConfigurationManager.AppSettings["DevID"];
            oContext.ApiCredential.ApiAccount.Certificate = ConfigurationManager.AppSettings["CertID"];

            // set the AuthToken
            oContext.ApiCredential.eBayToken = ConfigurationManager.AppSettings["Token"];

            oContext.SoapApiServerUrl = "https://api.ebay.com/wsapi";

            // set the Site of the Context
            oContext.Site = eBay.Service.Core.Soap.SiteCodeType.US;

            // very important, let's setup the logging
            ApiLogManager oLogManager = new ApiLogManager();
            oLogManager.ApiLoggerList.Add(new eBay.Service.Util.FileLogger("GetSellerList459NETSDK.log", true, true, true));
            oLogManager.EnableLogging = true;
            oContext.ApiLogManager = oLogManager;

            // the WSDL Version used for this SDK build
            oContext.Version = "459";

            // set the CallRetry properties
            CallRetry oCallRetry = new CallRetry();
            // set the delay between each retry to 1 millisecond
            oCallRetry.DelayTime = 1;
            // set the maximum number of retries
            oCallRetry.MaximumRetries = 3;
            // set the error codes on which to retry
            eBay.Service.Core.Soap.StringCollection oErrorCodes = new eBay.Service.Core.Soap.StringCollection();
            oErrorCodes.Add("10007"); // Internal error to the application ... general error
            oErrorCodes.Add("2"); // unsupported verb error
            oErrorCodes.Add("251"); // eBay Structured Exception ... general error
            oCallRetry.TriggerErrorCodes = oErrorCodes;
            // set the exception types on which to retry
            TypeCollection oExceptions = new TypeCollection();
            oExceptions.Add(typeof(System.Net.ProtocolViolationException));
            // the "Client found response content type of 'text/plain'" exception is of type SdkException, so let's add that to the list
            oExceptions.Add(typeof(SdkException));
            oCallRetry.TriggerExceptions = oExceptions;

            // set CallRetry back to ApiContext
            oContext.CallRetry = oCallRetry;

            // set the timeout to 2 minutes
            oContext.Timeout = 120000;

            GetSellerListCall oGetSellerListCall = new GetSellerListCall(oContext);

            // set the Version used in the call
            oGetSellerListCall.Version = oContext.Version;

            // set the Site of the call
            oGetSellerListCall.Site = oContext.Site;

            // enable the compression feature
            oGetSellerListCall.EnableCompression = true;

            // use GranularityLevel of Fine
            oGetSellerListCall.GranularityLevel = GranularityLevelCodeType.Fine;

            // get the first page, 200 items per page
            PaginationType oPagination = new PaginationType();
            oPagination.EntriesPerPage = 100;
            oPagination.EntriesPerPageSpecified = true;
            oPagination.PageNumber = 1;
            oPagination.PageNumberSpecified = true;
            oGetSellerListCall.Pagination = oPagination;

            // ask for all items that are ending in the future (active items)
            oGetSellerListCall.EndTimeFilter = new TimeFilter(DateTime.Now, DateTime.Now.AddMonths(3));
            oGetSellerListCall.UserID = seller;

            // return items that end soonest first
            oGetSellerListCall.Sort = 2;
            // see http://developer.ebay.com/DevZone/SOAP/docs/WSDL/xsd/1/element/1597.htm for Sort documentation

            var oItems = new ItemTypeCollection();
            oItems = null;
            try
            {
                oItems = oGetSellerListCall.GetSellerList();
            }
            catch (ApiException oApiEx)
            {
                // process exception ... pass to caller, implement retry logic here or in caller, whatever you want to do
                Console.WriteLine(oApiEx.Message);
                errMsg = oApiEx.Message;
            }
            catch (SdkException oSdkEx)
            {
                // process exception ... pass to caller, implement retry logic here or in caller, whatever you want to do
                Console.WriteLine(oSdkEx.Message);
                errMsg = oSdkEx.Message;
            }
            catch (Exception oEx)
            {
                // process exception ... pass to caller, implement retry logic here or in caller, whatever you want to do
                Console.WriteLine(oEx.Message);
                errMsg = oEx.Message;
            }
            return oItems;
        }

        public static void GetSellerListPrint(string seller)
        {
            var oItems = GetSellerList(seller, out string errMsg);
            if (oItems == null)
            {
                Console.WriteLine(errMsg);
                return;
            }

            int cnt = 0;
            // output some of the data
            foreach (ItemType oItem in oItems)
            {
                //if (oItem.SellingStatus.QuantitySold > 0)
                //{

                Console.WriteLine("ItemID: " + oItem.ItemID);
                Console.WriteLine("Title: " + oItem.Title);
                Console.WriteLine("Item type: " + oItem.ListingType.ToString());
                Console.WriteLine("Listing status: " + oItem.SellingStatus.ListingStatus);
                Console.WriteLine("Qty sold: " + oItem.SellingStatus.QuantitySold);
                if (0 < oItem.SellingStatus.BidCount)
                {
                    // The HighBidder element is valid only if there is at least 1 bid
                    Console.WriteLine("High Bidder is " + oItem.SellingStatus.HighBidder.UserID);
                }
                Console.WriteLine("Current Price is " + oItem.SellingStatus.CurrentPrice.currencyID.ToString() + " " + oItem.SellingStatus.CurrentPrice.Value.ToString());
                Console.WriteLine("End Time is " + oItem.ListingDetails.EndTime.ToLongDateString() + " " + oItem.ListingDetails.EndTime.ToLongTimeString());
                //}
                Console.WriteLine("count: " + (++cnt));
                Console.WriteLine("");

                // the data that is accessible through the item object
                // for different GranularityLevel and DetailLevel choices
                // can be found at the following URL:
                // http://developer.ebay.com/DevZone/SOAP/docs/WebHelp/GetSellerListCall-GetSellerList_Best_Practices.html
            }
            Console.WriteLine("Done");
        }

        // https://ebaydts.com/eBayKBDetails?KBid=1937
        //
        // also look at GetOrderTransactions()
        public static TransactionTypeCollection GetItemTransactions(string itemId, DateTime ModTimeFrom, DateTime ModTimeTo, ApplicationUser user)
        {
            dsmodels.DataModelsDB db = new dsmodels.DataModelsDB();
            var profile = db.GetUserProfile(user.Id);
            ApiContext oContext = new ApiContext();

            //set the dev,app,cert information
            oContext.ApiCredential.ApiAccount.Developer = profile.DevID;
            oContext.ApiCredential.ApiAccount.Application = profile.AppID;
            oContext.ApiCredential.ApiAccount.Certificate = profile.CertID;
            oContext.ApiCredential.eBayToken = profile.UserToken;

            //set the endpoint (sandbox) use https://api.ebay.com/wsapi for production
            oContext.SoapApiServerUrl = "https://api.ebay.com/wsapi";

            //set the Site of the Context
            oContext.Site = eBay.Service.Core.Soap.SiteCodeType.US;

            //the WSDL Version used for this SDK build
            oContext.Version = "817";

            //very important, let's setup the logging
            ApiLogManager oLogManager = new ApiLogManager();
            oLogManager.ApiLoggerList.Add(new eBay.Service.Util.FileLogger("GetItemTransactions.log", false, false, true));
            oLogManager.EnableLogging = true;
            oContext.ApiLogManager = oLogManager;

            GetItemTransactionsCall oGetItemTransactionsCall = new GetItemTransactionsCall(oContext);

            //' set the Version used in the call
            oGetItemTransactionsCall.Version = oContext.Version;

            //' set the Site of the call
            oGetItemTransactionsCall.Site = oContext.Site;

            //' enable the compression feature
            oGetItemTransactionsCall.EnableCompression = true;

            DateTime CreateTimeFromPrev;

            //ModTimeTo set to the current time
            //ModTimeTo = DateTime.Now.ToUniversalTime();

            //ts1 is 15 mins
            //TimeSpan ts1 = new TimeSpan(9000000000);
            //CreateTimeFromPrev = ModTimeTo.AddDays(-30);

            //Set the ModTimeFrom the last time you made the call minus 2 minutes
            //ModTimeFrom = CreateTimeFromPrev;

            //set ItemID and <DetailLevel>ReturnAll<DetailLevel>
            oGetItemTransactionsCall.ItemID = itemId;
            oGetItemTransactionsCall.DetailLevelList.Add(DetailLevelCodeType.ReturnAll);

            var r = oGetItemTransactionsCall.GetItemTransactions(itemId, ModTimeFrom, ModTimeTo);

            return r;
        }

        public static ApiAccessRuleTypeCollection GetAPIStatus(ApplicationUser user)
        {
            try
            {
                dsmodels.DataModelsDB db = new dsmodels.DataModelsDB();
                var profile = db.GetUserProfile(user.Id);
                ApiContext oContext = new ApiContext();

                //set the dev,app,cert information
                oContext.ApiCredential.ApiAccount.Developer = profile.DevID;
                oContext.ApiCredential.ApiAccount.Application = profile.AppID;
                oContext.ApiCredential.ApiAccount.Certificate = profile.CertID;
                oContext.ApiCredential.eBayToken = profile.UserToken;

                oContext.SoapApiServerUrl = "https://api.ebay.com/wsapi";

                //set the Site of the Context
                oContext.Site = eBay.Service.Core.Soap.SiteCodeType.US;

                //the WSDL Version used for this SDK build
                oContext.Version = "817";
                GetApiAccessRulesCall oGetApiAccessRulesCall = new GetApiAccessRulesCall(oContext);

                //' set the Version used in the call
                oGetApiAccessRulesCall.Version = oContext.Version;

                //' set the Site of the call
                oGetApiAccessRulesCall.Site = oContext.Site;

                //' enable the compression feature
                oGetApiAccessRulesCall.EnableCompression = true;
                var r = oGetApiAccessRulesCall.GetApiAccessRules();
                return r;
            }
            catch (Exception ex)
            {
                string s = ex.Message;
                return null;
            }
        }

        // note below that GetApiAccessRules returns a collection but first item is CallName, ApplicationAggregate, which returns all
        public static long GetTradingAPIUsage(ApplicationUser user)
        {
            try
            {
                dsmodels.DataModelsDB db = new dsmodels.DataModelsDB();
                var profile = db.GetUserProfile(user.Id);
                ApiContext oContext = new ApiContext();

                //set the dev,app,cert information
                oContext.ApiCredential.ApiAccount.Developer = profile.DevID;
                oContext.ApiCredential.ApiAccount.Application = profile.AppID;
                oContext.ApiCredential.ApiAccount.Certificate = profile.CertID;
                oContext.ApiCredential.eBayToken = profile.UserToken;

                oContext.SoapApiServerUrl = "https://api.ebay.com/wsapi";

                //set the Site of the Context
                oContext.Site = eBay.Service.Core.Soap.SiteCodeType.US;

                //the WSDL Version used for this SDK build
                oContext.Version = "817";
                GetApiAccessRulesCall oGetApiAccessRulesCall = new GetApiAccessRulesCall(oContext);

                //' set the Version used in the call
                oGetApiAccessRulesCall.Version = oContext.Version;

                //' set the Site of the call
                oGetApiAccessRulesCall.Site = oContext.Site;

                //' enable the compression feature
                oGetApiAccessRulesCall.EnableCompression = true;

                var r = oGetApiAccessRulesCall.GetApiAccessRules();
                var i = r[0].DailyUsage;
                return i;
            }
            catch (Exception ex)
            {
                string s = ex.Message;
                return -1;
            }
        }

        /// <summary>
        /// https://stackoverflow.com/questions/33095566/i-am-trying-to-check-for-expired-token-for-ebay-sdk-with-getclientalertsauthtoke
        /// </summary>
        /// <param name="user"></param>
        /// <returns></returns>
        public static TokenStatusTypeCustom GetTokenStatus(ApplicationUser user)
        {
            try
            {
                dsmodels.DataModelsDB db = new dsmodels.DataModelsDB();
                var profile = db.GetUserProfile(user.Id);
                ApiContext oContext = new ApiContext();

                //set the dev,app,cert information
                oContext.ApiCredential.ApiAccount.Developer = profile.DevID;
                oContext.ApiCredential.ApiAccount.Application = profile.AppID;
                oContext.ApiCredential.ApiAccount.Certificate = profile.CertID;
                oContext.ApiCredential.eBayToken = profile.UserToken;

                oContext.SoapApiServerUrl = "https://api.ebay.com/wsapi";

                //set the Site of the Context
                oContext.Site = eBay.Service.Core.Soap.SiteCodeType.US;

                //the WSDL Version used for this SDK build
                oContext.Version = "817";
                GetTokenStatusCall oGetTokenStatusCall = new GetTokenStatusCall(oContext);

                //' set the Version used in the call
                oGetTokenStatusCall.Version = oContext.Version;

                //' set the Site of the call
                oGetTokenStatusCall.Site = oContext.Site;

                //' enable the compression feature
                oGetTokenStatusCall.EnableCompression = true;

                var r = oGetTokenStatusCall.GetTokenStatus();
                TokenStatusTypeCustom final = new TokenStatusTypeCustom();
                final.StatusStr = r.Status.ToString();
                final.ExpirationTime = r.ExpirationTime;

                return final;
            }
            catch (Exception ex)
            {
                string s = ex.Message;
                return null;
            }
        }

        // https://ebaydts.com/eBayKBDetails?KBid=1987
        //
        // 192369073559
        protected static void GetItem_notused(string itemId)
        {
            ApiContext oContext = new ApiContext();

            //set the dev,app,cert information
            oContext.ApiCredential.ApiAccount.Developer = ConfigurationManager.AppSettings["devID"];
            oContext.ApiCredential.ApiAccount.Application = ConfigurationManager.AppSettings["appID"];
            oContext.ApiCredential.ApiAccount.Certificate = ConfigurationManager.AppSettings["certID"];

            //set the AuthToken
            oContext.ApiCredential.eBayToken = ConfigurationManager.AppSettings["ebayToken"];

            //set the endpoint (sandbox) use https://api.ebay.com/wsapi for production
            oContext.SoapApiServerUrl = "https://api.ebay.com/wsapi";

            //set the Site of the Context
            oContext.Site = eBay.Service.Core.Soap.SiteCodeType.US;

            //the WSDL Version used for this SDK build
            oContext.Version = "735";

            //very important, let's setup the logging
            ApiLogManager oLogManager = new ApiLogManager();
            oLogManager.ApiLoggerList.Add(new eBay.Service.Util.FileLogger("GetItem.log", true, true, true));
            oLogManager.EnableLogging = true;
            oContext.ApiLogManager = oLogManager;

            GetItemCall oGetItemCall = new GetItemCall(oContext);

            //' set the Version used in the call
            oGetItemCall.Version = oContext.Version;

            //' set the Site of the call
            oGetItemCall.Site = oContext.Site;

            //' enable the compression feature
            oGetItemCall.EnableCompression = true;

            oGetItemCall.DetailLevelList.Add(DetailLevelCodeType.ReturnAll);

            oGetItemCall.ItemID = itemId;

            var r = oGetItemCall.GetItem(oGetItemCall.ItemID);
            var sold = r.SellingStatus.QuantitySold;
            var pic = r.PictureDetails.PictureURL;
        }

        /// <summary>
        /// Get seller's shipping information.
        /// 10.07.2019 NOTE: While this works, not using it since overkill on every call.
        /// </summary>
        /// <param name="itemId"></param>
        /// <param name="appid"></param>
        /// <returns></returns>
        public static async Task<ShippingCostSummary> GetShippingCosts(string itemId, string appid)
        {
            try
            {
                var shippingCost = new ShippingCostSummary();
                Shopping svc = new Shopping();
                // set the URL and it's parameters
                svc.Url = string.Format("http://open.api.ebay.com/shopping?callname=GetShippingCosts&responseencoding=XML&appid={0}&siteid=0&version=517&ItemID={1}&DestinationCountryCode=US&DestinationPostalCode=95128&IncludeDetails=true&QuantitySold=1", appid, itemId);
                string uri = svc.Url;
                string errMsg;
                using (HttpClient httpClient = new HttpClient())
                {
                    string s = await httpClient.GetStringAsync(uri);
                    s = s.Replace("\"", "'");
                    string output = s.Replace(" xmlns='urn:ebay:apis:eBLBaseComponents'", string.Empty);

                    errMsg = GetSingleItemError(output);
                    if (!string.IsNullOrEmpty(errMsg))
                    {
                        throw new Exception(errMsg);
                    }
                    XElement root = XElement.Parse(output);
                    var qryRecords = from record in root.Elements("ShippingCostSummary")
                                     select record;

                    if (qryRecords.Count() > 0)
                    {
                        var r = (from r2 in qryRecords
                                 select new
                                 {
                                     ShippingServiceName = r2.Element("ShippingServiceName"),
                                     ShippingServiceCost = r2.Element("ShippingServiceCost")
                                 }).Single();
                        shippingCost.ShippingServiceName = r.ShippingServiceName.Value;
                        shippingCost.ShippingServiceCost = r.ShippingServiceCost.Value;
                        return shippingCost;
                    }
                }
                return null;
            }
            catch (Exception exc)
            {
                return null;
            }
        }

        // Purpose of GetSingleItem is to fetch properties such as a listing's description and photos
        // it is used when performing an auto-listing
        public static async Task<Listing> GetSingleItem(string itemId, string appid)
        {
            string errMsg = null;
            StringReader sr;
            string output;

            try
            {
                Models.DataModelsDB db = new Models.DataModelsDB();
                //var profile = db.UserProfiles.Find(user.Id);

                //CustomShoppingService service = new CustomShoppingService();
                //service.Url = "http://open.api.ebay.com/shopping";
                //service.appID = profile.AppID;
                //var request = new GetSingleItemRequestType();
                //request.ItemID = itemId;
                //var response = service.GetSingleItem(request);
                //return response;

                Shopping svc = new Shopping();
                // set the URL and it's parameters

                // docs for IncludeSelector
                // https://developer.ebay.com/devzone/shopping/docs/callref/getsingleitem.html
                svc.Url = string.Format("http://open.api.ebay.com/shopping?callname=GetSingleItem&IncludeSelector=Details,Description,ItemSpecifics,Variations&appid={0}&version=515&ItemID={1}", appid, itemId);
                // create a new request type
                GetSingleItemRequestType request = new GetSingleItemRequestType();
                // put in your own item number
                //request.ItemID = itemId;
                // we will request Details
                // for IncludeSelector reference see
                // http://developer.ebay.com/DevZone/shopping/docs/CallRef/GetSingleItem.html#detailControls
                //request.IncludeSelector = "Details";
                //request.IncludeSelector = "Details,Description,TextDescription";
                // create a new response type
                GetSingleItemResponseType response = new GetSingleItemResponseType();

                string uri = svc.Url;
                using (HttpClient httpClient = new HttpClient())
                {
                    string s = await httpClient.GetStringAsync(uri);
                    s = s.Replace("\"", "'");
                    output = s.Replace(" xmlns='urn:ebay:apis:eBLBaseComponents'", string.Empty);

                    #region Could not get xml deserialization to work - very annoying!
                    XmlSerializer x = new XmlSerializer(typeof(GetSingleItemResponseType));
                    //output = @"<?xml version='1.0' encoding='UTF-8'?>
                    //  <GetSingleItemResponse>
                    //   <Timestamp>2018-04-18T21:18:17.064Z</Timestamp>
                    //</GetSingleItemResponse>
                    //";
                    #endregion

                    errMsg = GetSingleItemError(output);
                    if (!string.IsNullOrEmpty(errMsg))
                    {
                        throw new Exception(errMsg);
                    }

                    XElement root = XElement.Parse(output);
                    var qryRecords = from record in root.Elements("Item")
                                     select record;
                    if (qryRecords.Count() == 0)
                    {
                        return null;
                    }
                    var r = (from r2 in qryRecords
                             select new
                             {
                                 Description = r2.Element("Description"),
                                 Title = r2.Element("Title"),
                                 Price = r2.Element("ConvertedCurrentPrice"),
                                 ListingUrl = r2.Element("ViewItemURLForNaturalSearch"),
                                 PrimaryCategoryID = r2.Element("PrimaryCategoryID"),
                                 PrimaryCategoryName = r2.Element("PrimaryCategoryName").ElementValueNull(),
                                 Quantity = r2.Element("Quantity"),
                                 QuantitySold = r2.Element("QuantitySold"),
                                 ListingStatus = r2.Element("ListingStatus"),
                                 Seller = r2.Element("Seller").Element("UserID"),
                                 Shipping = r2.Element("ShippingCostSummary").ElementValueNull()
                             }).Single();

                    // 10.10.2019 had case where no pictures being returned for a seller's listing - idk
                    // again, probably has something to do with variation listing?
                    // https://www.ebay.com/itm/XXL-Folding-Padded-Director-Chair-Heavy-Duty-W-Side-Table-Drink-Holder-500-Lb-/352687221867?var=
                    //
                    var list = qryRecords.Elements("PictureURL")
                            .Select(element => element.Value)
                            .ToArray();
                    if (list.Count() == 0)
                    {
                        list = qryRecords.Elements("Variations").Elements("Pictures").Elements("VariationSpecificPictureSet").Elements("PictureURL")
                            .Select(element => element.Value)
                            .ToArray();
                    }

                    var a = r.Shipping;

                    var si = new Listing();
                    /*
                     * 10.07.2019
                     * Good to know how to do this but not necessary since can mostly just look up the seller and see what his
                     * shipping policy is.  Don't need to exhaust API calls on this.
                    var shippingCost = await GetShippingCosts(itemId, appid);
                    si.ShippingServiceCost = shippingCost.ShippingServiceCost;
                    si.ShippingServiceName = shippingCost.ShippingServiceName;
                    */
                    si.PictureUrl = Util.ListToDelimited(list, ';');
                    si.Title = r.Title.Value;
                    si.Description = r.Description.Value;
                    si.SellerPrice = Convert.ToDecimal(r.Price.Value);
                    si.EbayUrl = r.ListingUrl.Value;
                    si.PrimaryCategoryID = r.PrimaryCategoryID.Value;
                    si.PrimaryCategoryName = r.PrimaryCategoryName;
                    int x1 = Convert.ToInt32(r.Quantity.Value);
                    int x2 = Convert.ToInt32(r.QuantitySold.Value);
                    si.Qty = x1 - x2;   // available qty; https://forums.developer.ebay.com/questions/11293/how-to-get-item-quantity-available.html
                    si.ListingStatus = r.ListingStatus.Value;
                    si.Seller = r.Seller.Value;
                    //si.Qty = Convert.ToInt32(r.Quantity.Value);
                    return si;
                }
            }
            catch (Exception exc)
            {
                string msg = "itemid: " + itemId.ToString() + " GetSingleItem " + exc.Message;
                dsutil.DSUtil.WriteFile(_logfile, msg, "nousername");
                throw;
            }
        }

        protected static string GetSingleItemError(string output)
        {
            string errMsg = null;
            try
            {
                var elem = XElement.Parse(output);
                string xmlErr = null;
                xmlErr = (string)elem.Element("Errors").Element("ShortMessage");
                if (!string.IsNullOrEmpty(xmlErr))
                {
                    errMsg = xmlErr;
                }
            }
            catch { }
            return errMsg;
        }

        // Was being developed when trying to get details of an item number
        // not fully tested
        // ended up with GetSingleItem() instead
        public static FindItemsAdvancedResponse FindByKeyword(ApplicationUser user)
        {
            dsmodels.DataModelsDB db = new dsmodels.DataModelsDB();

            // Setting the required proterty value

            CustomFindAdvanced service = new CustomFindAdvanced();
            service.Url = "http://svcs.ebay.com/services/search/FindingService/v1";
            var profile = db.GetUserProfile(user.Id);
            service.appID = profile.AppID;
            FindItemsAdvancedRequest request = new FindItemsAdvancedRequest();
            request.keywords = "302704549832";
            //var p = new ProductId();
            //p.type = "ReferenceID";
            ////p.type = "UPC";
            //p.Value = "222903428290";
            //p.Value = "302704549832";
            ////p.Value = "0019649215775";
            //request.productId = p;

            // Setting the pagination 
            PaginationInput pagination = new PaginationInput();
            pagination.entriesPerPageSpecified = true;
            pagination.entriesPerPage = 100;
            pagination.pageNumberSpecified = true;
            pagination.pageNumber = 1;
            request.paginationInput = pagination;

            FindItemsAdvancedResponse response = service.findItemsAdvanced(request);
            return response;
        }

        public static FindCompletedItemsResponse FindCompletedItems(string seller, int daysBack, string appID, int pageNumber)
        {
            try
            {
                CustomFindSold service = new CustomFindSold();
                service.Url = "http://svcs.ebay.com/services/search/FindingService/v1";
                service.appID = appID;
                FindCompletedItemsRequest request = new FindCompletedItemsRequest();

                ItemFilter filterSeller = new ItemFilter();
                filterSeller.name = ItemFilterType.Seller;
                filterSeller.paramName = "name";
                filterSeller.paramValue = "Seller";
                filterSeller.value = new string[] { seller };

                DateTime ModTimeTo = DateTime.Now.ToUniversalTime();
                DateTime ModTimeFrom = ModTimeTo.AddDays(-daysBack);
                string ModTimeToStr = ModTimeTo.Year + "-" + ModTimeTo.Month.ToString("00") + "-" + ModTimeTo.Day.ToString("00") + "T00:00:00.000Z";
                string ModTimeFromStr = ModTimeFrom.Year + "-" + ModTimeFrom.Month.ToString("00") + "-" + ModTimeFrom.Day.ToString("00") + "T00:00:00.000Z";

                ItemFilter filterEndTimeFrom = new ItemFilter();
                filterEndTimeFrom.name = ItemFilterType.EndTimeFrom;
                //filterEndTimeFrom.paramName = "name";
                //filterEndTimeFrom.paramValue = "EndTimeFrom";
                filterEndTimeFrom.value = new string[] { ModTimeFromStr };

                ItemFilter filterEndTimeTo = new ItemFilter();
                filterEndTimeTo.name = ItemFilterType.EndTimeTo;
                //filterEndTimeTo.paramName = "name";
                //filterEndTimeTo.paramValue = "filterEndTimeTo";
                filterEndTimeTo.value = new string[] { ModTimeToStr };

                ItemFilter filterSoldOnly = new ItemFilter();
                filterSoldOnly.name = ItemFilterType.SoldItemsOnly;
                filterSoldOnly.value = new string[] { "true" };

                //Create the filter array
                ItemFilter[] itemFilters = new ItemFilter[3];

                //Add Filters to the array
                itemFilters[0] = filterSeller;
                itemFilters[1] = filterEndTimeFrom;
                itemFilters[2] = filterEndTimeTo;

                request.itemFilter = itemFilters;

                // Setting the pagination 
                PaginationInput pagination = new PaginationInput();
                pagination.entriesPerPageSpecified = true;
                pagination.entriesPerPage = 100;
                pagination.pageNumberSpecified = true;
                pagination.pageNumber = pageNumber;
                request.paginationInput = pagination;

                // Sorting the result
                request.sortOrderSpecified = true;
                request.sortOrder = SortOrderType.EndTimeSoonest;

                FindCompletedItemsResponse response = service.findCompletedItems(request);

                int totalPages = response.paginationOutput.totalPages;
                //Console.WriteLine("Count: " + response.searchResult.count);

                if (response.searchResult.item != null)
                    return response;
                else return null;
            }
            catch (Exception ex)
            {
                throw (ex);
            }
        }

        // uses operation 'findItemsAdvanced'
        protected static void FindItems(string keyword)
        {
            StringBuilder strResult = new StringBuilder();
            try
            {
                CustomFindAdvanced service = new CustomFindAdvanced();
                service.Url = "http://svcs.ebay.com/services/search/FindingService/v1";
                FindItemsAdvancedRequest request = new FindItemsAdvancedRequest();

                // Setting the required proterty value
                //request.keywords = keyword.Trim();

                //Create Filter Objects
                ItemFilter filterEndTimeFrom = new ItemFilter();
                ItemFilter filterEndTimeTo = new ItemFilter();
                ItemFilter filterCatID = new ItemFilter();

                //Set Values for each filter
                filterEndTimeFrom.name = ItemFilterType.EndTimeFrom;
                filterEndTimeFrom.value = new string[] { "" };

                filterEndTimeTo.name = ItemFilterType.EndTimeTo;
                filterEndTimeTo.value = new string[] { "" };

                filterCatID.name = ItemFilterType.EndTimeFrom;
                filterCatID.value = new string[] { "" };

                ItemFilter filterSeller = new ItemFilter();
                filterSeller.name = ItemFilterType.Seller;
                filterSeller.paramName = "name";
                filterSeller.paramValue = "Seller";
                filterSeller.value = new string[] { "**justforyou**" };

                //Create the filter array
                ItemFilter[] itemFilters = new ItemFilter[1];

                //Add Filters to the array
                itemFilters[0] = filterSeller;
                //itemFilters[1] = filterEndTimeFrom;
                //itemFilters[2] = filterEndTimeTo;

                request.itemFilter = itemFilters;

                // Setting the pagination 
                PaginationInput pagination = new PaginationInput();
                pagination.entriesPerPageSpecified = true;
                pagination.entriesPerPage = 25;
                pagination.pageNumberSpecified = true;
                pagination.pageNumber = 1;
                request.paginationInput = pagination;

                // Sorting the result
                request.sortOrderSpecified = true;
                request.sortOrder = SortOrderType.CurrentPriceHighest;

                FindItemsAdvancedResponse response = service.findItemsAdvanced(request);
                if (response.searchResult.count > 0)
                {
                    foreach (SearchItem searchItem in response.searchResult.item)
                    {
                        strResult.AppendLine("ItemID: " + searchItem.itemId);
                        strResult.AppendLine("Title: " + searchItem.title);
                        strResult.AppendLine("Type: " + searchItem.listingInfo.listingType);
                        strResult.AppendLine("View: " + searchItem.viewItemURL);
                        strResult.AppendLine("Price: " + searchItem.sellingStatus.currentPrice.Value);
                        strResult.AppendLine("Picture: " + searchItem.galleryURL);
                        strResult.AppendLine("------------------------------------------------------------------------");
                    }
                }
                else
                {
                    strResult.AppendLine("No result found...Try with other keyword(s)");
                }
                Console.WriteLine("");
                Console.WriteLine(strResult.ToString());
                Console.WriteLine("Total Pages: " + response.paginationOutput.totalPages);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error: " + ex.Message);
            }
        }

        // uses operation 'findItemsByKeywords'
        protected static void SOAPSearch(string keyword)
        {
            StringBuilder strResult = new StringBuilder();
            try
            {
                CustomFindingService service = new CustomFindingService();
                service.Url = "http://svcs.ebay.com/services/search/FindingService/v1";
                FindItemsByKeywordsRequest request = new FindItemsByKeywordsRequest();

                // Setting the required proterty value
                request.keywords = keyword.Trim();

                // Setting the pagination 
                PaginationInput pagination = new PaginationInput();
                pagination.entriesPerPageSpecified = true;
                pagination.entriesPerPage = 25;
                pagination.pageNumberSpecified = true;
                pagination.pageNumber = 1;
                request.paginationInput = pagination;

                // Sorting the result
                request.sortOrderSpecified = true;
                request.sortOrder = SortOrderType.CurrentPriceHighest;

                FindItemsByKeywordsResponse response = service.findItemsByKeywords(request);
                if (response.searchResult.count > 0)
                {
                    foreach (SearchItem searchItem in response.searchResult.item)
                    {
                        strResult.AppendLine("ItemID: " + searchItem.itemId);
                        strResult.AppendLine("Title: " + searchItem.title);
                        strResult.AppendLine("Type: " + searchItem.listingInfo.listingType);
                        strResult.AppendLine("Price: " + searchItem.sellingStatus.currentPrice.Value);
                        strResult.AppendLine("Picture: " + searchItem.galleryURL);
                        strResult.AppendLine("------------------------------------------------------------------------");
                    }
                }
                else
                {
                    strResult.AppendLine("No result found...Try with other keyword(s)");
                }
                Console.WriteLine("");
                Console.WriteLine(strResult.ToString());
                Console.WriteLine("Total Pages: " + response.paginationOutput.totalPages);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error: " + ex.Message);
            }
        }

        /// <summary>
        /// Returns Completed Items
        /// </summary>
        /// <param name="seller"></param>
        /// <param name="daysBack"></param>
        /// <param name="user"></param>
        /// <returns></returns>
        public static int ItemCount(string seller, int daysBack, ApplicationUser user)
        {
            dsmodels.DataModelsDB db = new dsmodels.DataModelsDB();
            string _logfile = "scrape_log.txt";
            int notSold = 0;
            var listings = new List<Listing>();
            int totalCount = 0;

            CustomFindSold service = new CustomFindSold();
            service.Url = "http://svcs.ebay.com/services/search/FindingService/v1";
            var profile = db.GetUserProfile(user.Id);
            service.appID = profile.AppID;
            int currentPageNumber = 1;

            var request = BuildReqest(seller, daysBack);    // creates FindCompletedItemsRequest
            var response = ebayAPIs.GetResults(service, request, currentPageNumber);
            if (response.ack == AckValue.Success)
            {
                var result = response.searchResult;
                totalCount = result.count;
                if (result != null && result.count > 0)
                {
                    for (var i = response.paginationOutput.pageNumber; i < response.paginationOutput.totalPages; i++)
                    {
                        currentPageNumber += 1;

                        response = GetResults(service, request, currentPageNumber);
                        result = response.searchResult;
                        totalCount += result.count;
                    }
                }
            }
            return totalCount;
        }

        /// <summary>
        /// Essential function that fetch's seller's sales and stores.
        /// </summary>
        /// <param name="seller"></param>
        /// <param name="daysBack"></param>
        /// <param name="user"></param>
        /// <param name="rptNumber"></param>
        /// <returns></returns>
        public static async Task<ModelView> ToStart(string seller, int daysBack, ApplicationUser user, int rptNumber)
        {
            dsmodels.DataModelsDB db = new dsmodels.DataModelsDB();
            string _logfile = "scrape_log.txt";
            int notSold = 0;
            var listings = new List<Listing>();
            try
            {
                CustomFindSold service = new CustomFindSold();
                service.Url = "http://svcs.ebay.com/services/search/FindingService/v1";

                // don't put actual db code here - need function to return profile object
                var profile = db.GetUserProfile(user.Id);
                service.appID = profile.AppID;
                int currentPageNumber = 1;

                var request = BuildReqest(seller, daysBack);
                dsutil.DSUtil.WriteFile(_logfile, "Retrieve sales for " + seller, user.UserName);
                var response = ebayAPIs.GetResults(service, request, currentPageNumber);
                dsutil.DSUtil.WriteFile(_logfile, "Retrieve sales complete", user.UserName);

                if (response.ack == AckValue.Success)
                {
                    var result = response.searchResult;
                    if (result != null && result.count > 0)
                    {
                        // store the sales
                        await StoreTransactions(result, daysBack, user, rptNumber, listings, currentPageNumber);

                        // are there more pages of results?
                        for (var i = response.paginationOutput.pageNumber; i < response.paginationOutput.totalPages; i++)
                        {
                            currentPageNumber += 1;

                            response = GetResults(service, request, currentPageNumber);
                            result = response.searchResult;
                            await StoreTransactions(result, daysBack, user, rptNumber, listings, currentPageNumber);
                        }
                    }
                    var mv = new ModelView();
                    mv.Listings = listings;

                    int b = notSold;
                    return mv;
                }
                return null;
            }
            catch (Exception exc)
            {
                string msg = " ToStart " + exc.Message;
                dsutil.DSUtil.WriteFile(_logfile, msg, user.UserName);
                return null;
            }
        }

        /// <summary>
        /// Store seller's transactions for a page of results 
        /// </summary>
        /// <param name="result"></param>
        /// <param name="daysBack"></param>
        /// <param name="user"></param>
        /// <param name="rptNumber"></param>
        /// <param name="listings"></param>
        /// <param name="pg"></param>
        /// <returns></returns>
        protected static async Task StoreTransactions(SearchResult result, int daysBack, ApplicationUser user, int rptNumber, List<Listing> listings, int pg)
        {
            string _logfile = "scrape_log.txt";
            int notSold = 0;

            dsutil.DSUtil.WriteFile(_logfile, "StoreTransactions Start", user.UserName);
            UserProfileView profile;
            using (var db = new dsmodels.DataModelsDB())
            {
                profile = db.GetUserProfile(user.Id);
            }

            // Iterate completed items
            foreach (SearchItem searchItem in result.item)
            {
                using (var db = new dsmodels.DataModelsDB())
                {
                    var f = db.SearchHistory.Where(p => p.Id == rptNumber).FirstOrDefault();
                    if (f != null)
                    {
                        if (f.Running.HasValue)
                        {
                            if (!f.Running.Value)
                            {
                                return;
                            }
                        }
                    }
                }

                // The SearchResult that was passed to this method may have more than result per item number if different variations have sold.
                // But when pulling Transactions, we pull all per item number so only need to process each item number once.
                bool exists = listings.Any(item => item.ItemId == searchItem.itemId);
                if (!exists)
                {
                    var i = await ebayAPIs.GetSingleItem(searchItem.itemId, profile.AppID); // pulling this for ListingStatus
                                                                                            //var a = searchItem.itemId;
                                                                                            //var b = searchItem.title;
                                                                                            //var c = searchItem.listingInfo.listingType;
                                                                                            //var d = searchItem.viewItemURL;
                                                                                            //var e = searchItem.sellingStatus.currentPrice.Value;
                                                                                            //var f = searchItem.galleryURL;
                                                                                            //var g = searchItem.sellingStatus;
                                                                                            //var h = searchItem.sellingStatus.timeLeft;
                    var isVariation = searchItem.isMultiVariationListing;

                    var listing = new Listing();
                    listing.Title = searchItem.title;
                    listing.ItemId = searchItem.itemId;

                    // loop through each order
                    DateTime ModTimeTo = DateTime.Now.ToUniversalTime();
                    DateTime ModTimeFrom = ModTimeTo.AddDays(-daysBack);
                    TransactionTypeCollection transactions = null;
                    try
                    {
                        // We have queried for only sold times, but sometimes this returns nothing, possibly due to date range.
                        // Or may happen because of this:
                        // 'This listing was ended by the seller because the item is no longer available.'

                        dsutil.DSUtil.WriteFile(_logfile, "Get transactions for " + searchItem.itemId, user.UserName);
                        transactions = ebayAPIs.GetItemTransactions(searchItem.itemId, ModTimeFrom, ModTimeTo, user);
                        dsutil.DSUtil.WriteFile(_logfile, "Get transactions complete", user.UserName);

                        var orderHistory = new List<OrderHistory>();

                        // Iterate transactions for an item
                        foreach (TransactionType item in transactions)
                        {
                            // did it sell?
                            if (item.MonetaryDetails != null)
                            {
                                var pmtTime = item.MonetaryDetails.Payments.Payment[0].PaymentTime;
                                var pmtAmt = item.MonetaryDetails.Payments.Payment[0].PaymentAmount.Value;
                                var order = new OrderHistory();
                                order.Title = searchItem.title;
                                order.Qty = item.QuantityPurchased.ToString();

                                if (item.TransactionPrice == null)
                                {
                                    // is this bcs sellerPaidStatus="notpaid"?
                                    order.SellerPrice = "0.0";
                                    dsutil.DSUtil.WriteFile(_logfile, string.Format("StoreTransactions: item.TransactionPrice == null for item: {0}", searchItem.itemId), user.UserName);
                                }
                                else
                                {
                                    order.SellerPrice = item.TransactionPrice.Value.ToString();
                                }
                                // dsutil.DSUtil.WriteFile(_logfile, string.Format("Seller price: {0}", order.SellerPrice), user.UserName);

                                order.DateOfPurchase = item.CreatedDate;

                                order.EbayUrl = searchItem.viewItemURL;
                                // dsutil.DSUtil.WriteFile(_logfile, "order.EbayUrl complete", user.UserName);

                                order.ImageUrl = searchItem.galleryURL;
                                dsutil.DSUtil.WriteFile(_logfile, "order.ImageUrl complete", user.UserName);

                                var pictures = searchItem.pictureURLLarge;
                                // dsutil.DSUtil.WriteFile(_logfile, "pictures complete", user.UserName);

                                order.PageNumber = pg;
                                order.ItemId = searchItem.itemId;
                                order.SellingState = searchItem.sellingStatus.sellingState;
                                order.ListingStatus = i.ListingStatus;
                                order.IsMultiVariationListing = isVariation;

                                // order.ShippingServiceCost = i.ShippingServiceCost;
                                // order.ShippingServiceName = i.ShippingServiceName;

                                orderHistory.Add(order);
                            }
                            else
                            {
                                // i don't see this ever being executed which makes sense if querying only sold items
                                dsutil.DSUtil.WriteFile(_logfile, "Unexpected: item.MonetaryDetails == null", user.UserName);
                            }
                        }
                        if (transactions.Count == 0)
                        {
                            // Despite filtering for only sold items, we may still meet this condition (which doesn't make a whole lot of sense)
                            // in testing, I would see an item like 
                            // 'Test listing - DO NOT BID OR BUY362254235623'
                            //
                            ++notSold;
                        }

                        using (var db = new dsmodels.DataModelsDB())
                        {
                            db.OrderHistorySave(orderHistory, rptNumber, false);
                        }
                        dsutil.DSUtil.WriteFile(_logfile, "OrderHistorySave complete", user.UserName);
                        listing.Orders = orderHistory;
                        listings.Add(listing);

                        dsutil.DSUtil.WriteFile(_logfile, "StoreTransactions Complete", user.UserName);

                    }
                    catch (Exception exc)
                    {
                        string msg = " StoreTransactions " + exc.Message;
                        dsutil.DSUtil.WriteFile(_logfile, msg, user.UserName);
                        throw;
                    }
                }
            }
        }

        protected static string FormateBayTime(DateTime dt)
        {
            string dtStr = dt.Year + "-" + dt.Month.ToString("00") + "-" + dt.Day.ToString("00") + "T" + dt.Hour.ToString("00") + ":" + dt.Minute.ToString("00") + ":00.000Z";
            return dtStr;
        }

        /// <summary>
        /// Build request to fetch seller's sales
        /// 07.05.2019 needs further clarification - eBay told me a completed listing is a listing that ended which may or may not have sold,
        /// but that's not what this returns.
        /// </summary>
        /// <param name="seller"></param>
        /// <param name="daysBack"></param>
        /// <returns></returns>
        protected static FindCompletedItemsRequest BuildReqest(string seller, int daysBack)
        {
            FindCompletedItemsRequest request = new FindCompletedItemsRequest();

            ItemFilter filterSeller = new ItemFilter();
            filterSeller.name = ItemFilterType.Seller;
            //filterSeller.paramName = "name";
            //filterSeller.paramValue = "Seller";
            filterSeller.value = new string[] { seller };

            DateTime ModTimeTo = DateTime.Now.ToUniversalTime();
            DateTime ModTimeFrom = ModTimeTo.AddDays(-daysBack);
            string ModTimeToStr = FormateBayTime(ModTimeTo);
            string ModTimeFromStr = FormateBayTime(ModTimeFrom);

            //DateTime ModTimeTo = DateTime.Now;
            //DateTime ModTimeFrom = ModTimeTo.AddDays(-daysBack);
            //string ModTimeToStr = ModTimeTo.ToString();
            //string ModTimeFromStr = ModTimeFrom.ToString();

            ItemFilter filterEndTimeFrom = new ItemFilter();
            filterEndTimeFrom.name = ItemFilterType.EndTimeFrom;
            //filterEndTimeFrom.paramName = "name";
            //filterEndTimeFrom.paramValue = "EndTimeFrom";
            filterEndTimeFrom.value = new string[] { ModTimeFromStr };

            ItemFilter filterEndTimeTo = new ItemFilter();
            filterEndTimeTo.name = ItemFilterType.EndTimeTo;
            //filterEndTimeTo.paramName = "name";
            //filterEndTimeTo.paramValue = "filterEndTimeTo";
            filterEndTimeTo.value = new string[] { ModTimeToStr };

            ItemFilter filterSoldOnly = new ItemFilter();
            filterSoldOnly.name = ItemFilterType.SoldItemsOnly;
            filterSoldOnly.value = new string[] { "true" };

            //Create the filter array
            ItemFilter[] itemFilters = new ItemFilter[3];

            //Add Filters to the array
            itemFilters[0] = filterSeller;
            itemFilters[1] = filterEndTimeFrom;
            itemFilters[2] = filterEndTimeTo;
            //itemFilters[3] = filterSoldOnly;

            request.itemFilter = itemFilters;
            return request;
        }

        /// <summary>
        /// Actual fetch of seller's completed items
        /// </summary>
        /// <param name="service"></param>
        /// <param name="request"></param>
        /// <param name="currentPageNumber"></param>
        /// <returns></returns>
        public static FindCompletedItemsResponse GetResults(CustomFindSold service, FindCompletedItemsRequest request, int currentPageNumber)
        {
            request.paginationInput = GetNextPage(currentPageNumber);
            return service.findCompletedItems(request);
        }

        private static PaginationInput GetNextPage(int pageNumber)
        {
            return new PaginationInput
            {
                entriesPerPageSpecified = true,
                entriesPerPage = 100,
                pageNumberSpecified = true,
                pageNumber = pageNumber
            };
        }
    }
}