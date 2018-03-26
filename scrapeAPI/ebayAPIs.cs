using eBay.Service.Call;
using eBay.Service.Core.Sdk;
using eBay.Service.Core.Soap;
using scrapeAPI.com.ebay.developer;
using System;
using System.Configuration;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using scrapeAPI.Models;

namespace scrapeAPI
{
    public class ebayAPIs
    {

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
        protected static void GetSellerList_notused(string seller)
        {
            // TODO: Add code to start application here
            //
            // first, set up the ApiContext object
            ApiContext oContext = new ApiContext();

            // set the dev,app,cert information
            oContext.ApiCredential.ApiAccount.Developer = ConfigurationManager.AppSettings["devID"];
            oContext.ApiCredential.ApiAccount.Application = ConfigurationManager.AppSettings["appID"];
            oContext.ApiCredential.ApiAccount.Certificate = ConfigurationManager.AppSettings["certID"];

            // set the AuthToken
            oContext.ApiCredential.eBayToken = ConfigurationManager.AppSettings["ebayToken"];

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
            oPagination.EntriesPerPage = 200;
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

            try
            {
                ItemTypeCollection oItems = oGetSellerListCall.GetSellerList();
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
                    Console.WriteLine("");

                    // the data that is accessible through the item object
                    // for different GranularityLevel and DetailLevel choices
                    // can be found at the following URL:
                    // http://developer.ebay.com/DevZone/SOAP/docs/WebHelp/GetSellerListCall-GetSellerList_Best_Practices.html
                }
                Console.WriteLine("Done");
            }
            catch (ApiException oApiEx)
            {
                // process exception ... pass to caller, implement retry logic here or in caller, whatever you want to do
                Console.WriteLine(oApiEx.Message);
                return;
            }
            catch (SdkException oSdkEx)
            {
                // process exception ... pass to caller, implement retry logic here or in caller, whatever you want to do
                Console.WriteLine(oSdkEx.Message);
                return;
            }
            catch (Exception oEx)
            {
                // process exception ... pass to caller, implement retry logic here or in caller, whatever you want to do
                Console.WriteLine(oEx.Message);
                return;
            }
        }

        // https://ebaydts.com/eBayKBDetails?KBid=1937
        //
        // also look at GetOrderTransactions()
        public static TransactionTypeCollection GetItemTransactions(string itemId, DateTime ModTimeFrom, DateTime ModTimeTo, ApplicationUser user)
        {
            DataModelsDB db = new DataModelsDB();
            var profile = db.UserProfiles.Find(user.Id);
            ApiContext oContext = new ApiContext();

            //set the dev,app,cert information
            oContext.ApiCredential.ApiAccount.Developer = profile.DevID;
            oContext.ApiCredential.ApiAccount.Application = profile.AppID;
            oContext.ApiCredential.ApiAccount.Certificate = profile.CertID;
            oContext.ApiCredential.eBayToken = profile.UserToken;
            //oContext.ApiCredential.ApiAccount.Developer = ConfigurationManager.AppSettings["devID"];
            //oContext.ApiCredential.ApiAccount.Application = ConfigurationManager.AppSettings["appID"];
            //oContext.ApiCredential.ApiAccount.Certificate = ConfigurationManager.AppSettings["certID"];
            //oContext.ApiCredential.eBayToken = ConfigurationManager.AppSettings["ebayToken"];

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
                DataModelsDB db = new DataModelsDB();
                var profile = db.UserProfiles.Find(user.Id);
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

        public static long GetTradingAPIUsage(ApplicationUser user)
        {
            try
            {
                DataModelsDB db = new DataModelsDB();
                var profile = db.UserProfiles.Find(user.Id);
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

        public static SearchItem[] FindCompletedItems(string seller, int daysBack, string appID)
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
                pagination.entriesPerPage = 200;
                pagination.pageNumberSpecified = true;
                pagination.pageNumber = 1;
                request.paginationInput = pagination;

                // Sorting the result
                request.sortOrderSpecified = true;
                request.sortOrder = SortOrderType.EndTimeSoonest;

                FindCompletedItemsResponse response = service.findCompletedItems(request);

                //Console.WriteLine("Total Pages: " + response.paginationOutput.totalPages);
                //Console.WriteLine("Count: " + response.searchResult.count);
                return response.searchResult.item;
            }
            catch (Exception ex)
            {
                Console.WriteLine("FindCompletedItems: " + ex.Message);
                return null;
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

    }
}