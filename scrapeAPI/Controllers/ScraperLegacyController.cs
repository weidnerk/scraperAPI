﻿using HtmlAgilityPack;
using Newtonsoft.Json;
using scrapeAPI.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using System.Web.Http;

namespace scrapeAPI.Controllers
{
    public class ScraperLegacyController : ApiController
    {
        const string _logfile = "scrape_log.txt";
        DataModelsDB db = new DataModelsDB();
        const string _filename = "order.csv";

        [Route("numitems")]
        [HttpGet]
        public async Task<IHttpActionResult> GetNumItems(string seller, int daysBack, int waitSeconds, int resultsPerPg, int rptNumber, int minSold, string showNoOrders, string useProxy)
        {
            try
            {
                string header = string.Format("Seller: {0} daysBack: {1} waitSeconds: {2} resultsPerPg: {3}", seller, daysBack, waitSeconds, resultsPerPg);

                var listings = await GetProductListings(seller, daysBack, waitSeconds, resultsPerPg, rptNumber, minSold, showNoOrders, useProxy);

                if (listings != null)
                    return Ok(listings.Count());
                else return Ok(0);
            }
            catch (Exception exc)
            {
                return Content(HttpStatusCode.ExpectationFailed, DateTime.Now.ToString() + " GetNumItems " + exc.Message);
            }
        }

        protected async Task<List<HtmlNode>> GetProductListings(string seller, int daysBack, int waitSeconds, int resultsPerPg, int rptNumber, int minSold, string showNoOrders, string useProxy)
        {
            string baseDir = System.AppDomain.CurrentDomain.BaseDirectory;
            string log = baseDir + _logfile;
            try
            {

                //works better with the smaller seller
                string url = string.Format("https://www.ebay.com/csc/{0}/m.html?_ipg=48&_since=15&_sop=13&LH_Complete=1&LH_Sold=1&rt=nc&_trksid=p2046732.m1684", seller);

                //works better with the bigger sellers
                url = string.Format("https://www.ebay.com/csc/m.html?_since={0}&_sop=13&LH_Complete=1&LH_Sold=1&_ssn={1}&_ipg={2}&rt=nc", daysBack, seller, resultsPerPg);

                var httpClient = CreateHttpClient(useProxy);
                //var httpClient = new HttpClient();
                var html = await httpClient.GetStringAsync(url);

                var htmlDocument = new HtmlDocument();
                htmlDocument.LoadHtml(html);

                // Get html corresponding to 'ul' that contains the listings
                var ProductsHtml = htmlDocument.DocumentNode.Descendants("ul")
                    .Where(node => node.GetAttributeValue("id", "")
                    .Equals("ListViewInner")).ToList();

                if (ProductsHtml.Count() > 0)
                {
                    // put 'li's into actual list
                    var ProductListItems = ProductsHtml[0].Descendants("li")
                        .Where(node => node.GetAttributeValue("id", "")
                        .Contains("item")).ToList();
                    return ProductListItems;
                }
                else return null;
            }
            catch (Exception exc)
            {
                HomeController.WriteFile(log, "GetProductListings " + exc.Message);
                return null;
            }
        }

        [Route("scraper")]
        [HttpGet]
        public async Task<IHttpActionResult> GetSeller(string seller, int daysBack, int waitSeconds, int resultsPerPg, int rptNumber, int minSold, string showNoOrders, string useProxy)
        {
            string baseDir = System.AppDomain.CurrentDomain.BaseDirectory;
            string log = baseDir + _logfile;
            string header = string.Format("Seller: {0} daysBack: {1} waitSeconds: {2} resultsPerPg: {3}", seller, daysBack, waitSeconds, resultsPerPg);
            HomeController.WriteFile(log, header);

            return await GetSellerAsync(seller, daysBack, waitSeconds, resultsPerPg, rptNumber, minSold, showNoOrders, useProxy);
        }

        //public async Task<List<OrderHistory>> GetSellerAsync(string seller, int daysBack)
        protected async Task<IHttpActionResult> GetSellerAsync(string seller, int daysBack, int waitSeconds, int resultsPerPg, int rptNumber, int minSold, string showNoOrders, string useProxy)
        {
            string baseDir = System.AppDomain.CurrentDomain.BaseDirectory;
            string log = baseDir + _logfile;
            string fn = baseDir + _filename;
            int itemCount = 0;

            HttpResponseMessage message = Request.CreateResponse<ModelView>(HttpStatusCode.NoContent, null);
            try
            {
                var model = new ModelView();
                //model.Orders = new OrderHistory
                DateTime startTime = DateTime.Now;

                // generate sold listings for a seller
                //string seller = "theadkays";
                //seller = "jpurse83";
                //seller = "marketmajor";
                //seller = "shopwithusnow";
                //seller = "**justforyou**";
                //seller = "pricedrightsales";
                //seller = "lj_cyber_electron";
                //seller = "mordensmartfurniturestore";   // most sales I've seen, do have some errors
                //seller = "demi2601";

                int i = 0;
                var ProductListItems = await GetProductListings(seller, daysBack, waitSeconds, resultsPerPg, rptNumber, minSold, showNoOrders, useProxy);
                if (ProductListItems != null)
                {
                    if (ProductListItems.Count() > 0)
                    {
                        itemCount = ProductListItems.Count();
                        foreach (var ProductListItem in ProductListItems)
                        {
                            try
                            {
                                var listing = await ProcessListing(ProductListItem, daysBack, useProxy);
                                if (listing.Orders.Count() > 0)
                                {
                                    if (model.Listings == null)
                                        model.Listings = new List<Listing>();
                                    model.Listings.Add(listing);
                                    model.TotalOrders += listing.Orders.Count();

                                    db.OrderHistorySave(listing.Orders, rptNumber, false);

                                    if (showNoOrders == "0") ++model.MatchedListings;
                                }
                                else
                                {
                                    // listing ended
                                    var l = new Listing();
                                    l.Orders = new List<OrderHistory>();
                                    l.Orders.Add(new Models.OrderHistory { Title = " No orders found in range - " + listing.Title, Url = listing.Url });
                                    db.OrderHistorySave(l.Orders, rptNumber, true);
                                }
                                if (showNoOrders == "1") ++model.MatchedListings;
                                ++model.PercentTotalItemsProcesssed;
                                ++i;

                                Random r = new Random();
                                waitSeconds = r.Next(1, 5);
                                System.Threading.Thread.Sleep(waitSeconds * 1000);
                            }
                            catch (Exception exc)
                            {
                                HomeController.WriteFile(log, "SKIP " + exc.Message);
                                --itemCount;
                            }
                        }
                        HomeController.WriteFile(log, DateTime.Now.ToString() + " Completed.");
                    }
                }

                #region WriteToFile
                // write items to a file
                /*
                File.Delete(fn);
                if (ph != null)
                {
                    foreach (var item in ph)
                    {
                        try
                        {
                            //string order = string.Format("\"{0}\",{1},{2},{3}", title, item.Price, item.Qty, item.DateOfPurchase);
                            string order = string.Format("\"{0}\",{1},{2},{3}", item.Title, item.Price, item.Qty, item.DateOfPurchase);
                            WriteFile(fn, order);
                            //Console.WriteLine(order);
                        }
                        catch (Exception exc)
                        {
                            WriteFile(log, "WRITEFILE " + item.Title + " " + exc.Message);
                        }
                    }
                }
                */
                #endregion

                string footer = string.Format("Complete scan -> TotalItems: {0} TotalOrders: {1} Seconds: {2}", model.MatchedListings, model.TotalOrders, model.ElapsedSeconds);
                HomeController.WriteFile(log, footer);

                model.ElapsedSeconds = (DateTime.Now - startTime).TotalSeconds;
                return Ok(model);

            }
            catch (Exception exc)
            {
                HomeController.WriteFile(log, "GetSellerAsync " + exc.Message);
                return Content(HttpStatusCode.ExpectationFailed, DateTime.Now.ToString() + " GetSellerAsync " + exc.Message);
            }
        }

        // 'listing' is an li html node element
        protected async Task<Listing> ProcessListing(HtmlNode htmllisting, int daysBack, string useProxy)
        {
            string prodName = null;
            //var mv = new ModelView();

            string listingId = htmllisting.GetAttributeValue("listingid", "");

            //product name
            prodName = htmllisting.Descendants("h3")
                .Where(node => node.GetAttributeValue("class", "")
                .Equals("lvtitle")).FirstOrDefault().InnerText.Trim('\r', '\n', '\t');
            prodName = System.Web.HttpUtility.HtmlDecode(prodName);

            //url of item that will contain a hyperlink like '1 sold'
            //we will still need to click into that hyperlink for order details
            string detailUrl = htmllisting.Descendants("a").FirstOrDefault().GetAttributeValue("href", "").Trim('\r', '\n', '\t');

            // A sold item might be returned but i can't reach order history since the listing has ended,
            // the error trap will catch it and we continue
            var listing = new Listing();
            listing = await GenerateQtySold(prodName, detailUrl, daysBack, useProxy);
            listing.Title = prodName;
            listing.Url = detailUrl;
            return listing;
        }

        // browse to the listing and qty sold link
        // will error if listing has ended
        private async Task<Listing> GenerateQtySold(string title, string url, int daysBack, string useProxy)
        {
            try
            {
                var httpClient = CreateHttpClient(useProxy);
                //var httpClient = new HttpClient();
                var html = await httpClient.GetStringAsync(url);

                var htmlDocument = new HtmlDocument();
                htmlDocument.LoadHtml(html);

                var ProductsHtml = htmlDocument.DocumentNode.Descendants("span")
                    .Where(node => node.GetAttributeValue("class", "")
                    .Equals("vi-qtyS  vi-bboxrev-dsplblk vi-qty-vert-algn vi-qty-pur-lnk")).ToList();

                if (ProductsHtml.Count() == 0)
                {
                    ProductsHtml = htmlDocument.DocumentNode.Descendants("span")
                        .Where(node => node.GetAttributeValue("class", "")
                        .Equals("vi-qtyS-hot-red  vi-bboxrev-dsplblk vi-qty-vert-algn vi-qty-pur-lnk")).ToList();
                    if (ProductsHtml.Count() == 0)
                    {
                        ProductsHtml = htmlDocument.DocumentNode.Descendants("span")
                            .Where(node => node.GetAttributeValue("class", "")
                            .Equals("vi-qtyS-hot-red  vi-qty-vert-algn vi-qty-pur-lnk")).ToList();
                    }
                    if (ProductsHtml.Count() == 0)
                    {
                        ProductsHtml = htmlDocument.DocumentNode.Descendants("span")
                            .Where(node => node.GetAttributeValue("class", "")
                            .Equals("vi-qtyS  vi-qty-vert-algn vi-qty-pur-lnk")).ToList();
                    }
                    if (ProductsHtml.Count() == 0)
                    {
                        ProductsHtml = htmlDocument.DocumentNode.Descendants("span")
                            .Where(node => node.GetAttributeValue("class", "")
                            .Equals("vi-qtyS-hot  vi-qty-vert-algn vi-qty-pur-lnk")).ToList();
                    }
                    if (ProductsHtml.Count() == 0)
                    {
                        ProductsHtml = htmlDocument.DocumentNode.Descendants("span")
                            .Where(node => node.GetAttributeValue("class", "")
                            .Equals("vi-qtyS-hot  vi-bboxrev-dsplblk vi-qty-vert-algn vi-qty-pur-lnk")).ToList();
                    }
                }
                string lnk = ProductsHtml[0].Descendants("a").FirstOrDefault().GetAttributeValue("href", "").Trim('\r', '\n', '\t');

                // get image
                var imgHtml = htmlDocument.DocumentNode.Descendants("img")
                    .Where(node => node.GetAttributeValue("id", "")
                    .Equals("icImg")).ToList();
                string imgSrc = imgHtml[0].GetAttributeValue("src", "").Trim('\r', '\n', '\t');

                var listing = new Listing();
                listing = await OrderHistory(title, lnk, daysBack, url, imgSrc, useProxy);
                return listing;
            }
            catch (Exception exc)
            {
                Console.WriteLine("GenerateQtySold: " + title + " " + exc.Message);
                return null;
            }
        }

        // capture order history
        private async Task<Listing> OrderHistory(string title, string url, int daysBack, string detailUrl, string imgUrl, string useProxy)
        {
            var history = new List<OrderHistory>();

            string browse = System.Web.HttpUtility.HtmlDecode(url);

            //var httpClient = new HttpClient();
            var httpClient = CreateHttpClient(useProxy);
            var html = await httpClient.GetStringAsync(browse);

            var htmlDocument = new HtmlDocument();
            htmlDocument.LoadHtml(html);

            var ProductsHtml = htmlDocument.DocumentNode.Descendants("td")
                .Where(node => node.GetAttributeValue("class", "")
                .Equals("contentValueFont")).ToList();

            int i = 0;
            string price = null;
            string qty = null;
            string dateofpurchase = null;
            foreach (var item in ProductsHtml)
            {
                ++i;
                if (i == 1)
                {
                    price = item.InnerText;
                }
                if (i == 2)
                {
                    qty = item.InnerText;
                }
                if (i == 3)
                {
                    dateofpurchase = item.InnerText;

                    var h = new OrderHistory();
                    h.Title = title;
                    h.Price = price;
                    h.Qty = qty;
                    h.Url = detailUrl;
                    h.ImageUrl = imgUrl;
                    //h.DateOfPurchaseStr = dateofpurchase.Substring(0, dateofpurchase.Length - 4);
                    //var dt = Convert.ToDateTime(h.DateOfPurchaseStr);
                    //h.DateOfPurchase = dt;
                    //double numDays = (DateTime.Today - dt).TotalDays;
                    //if (numDays <= daysBack)
                    //    history.Add(h);
                    i = 0;
                }
            }
            var listing = new Listing();
            listing.Orders = history;
            return listing;
        }

        protected HttpClient CreateHttpClient(string useProxy)
        {
            if (useProxy == "1")
            {

                string proxyUri = string.Format("{0}:{1}", "us-wa.proxymesh.com", "31280");

                NetworkCredential proxyCreds = new NetworkCredential(
                    "ventures2018",
                    "k3918834"
                );

                WebProxy proxy = new WebProxy(proxyUri, false)
                {
                    UseDefaultCredentials = false,
                    Credentials = proxyCreds,
                };

                // Now create a client handler which uses that proxy

                HttpClient client = null;
                HttpClientHandler httpClientHandler = new HttpClientHandler()
                {
                    Proxy = proxy,
                    PreAuthenticate = true,
                    UseDefaultCredentials = false,
                };

                // You only need this part if the server wants a username and password:

                //string
                //    httpUserName = "?????",
                //    httpPassword = "secret";

                //httpClientHandler.Credentials = new NetworkCredential(httpUserName, httpPassword);

                client = new HttpClient(httpClientHandler);

                return client;
            }
            else
            {
                var client = new HttpClient();
                return client;
            }
        }
        #region ** testing **
        [HttpGet]
        [Route("multipart")]
        public HttpResponseMessage GetMultipartData()
        {
            // This is working 'async' when called directly from browser.
            // in other words, you start seeing results without waiting until the end.

            // But that's not what happens when called from angular, not seeing results being drawn immediately -
            // but isn't that what Observable is for?

            var response = new HttpResponseMessage();
            var content = new PushStreamContent(new Action<Stream, HttpContent, TransportContext>(WriteContent), "application/json");

            response.Headers.TransferEncodingChunked = true;
            response.Content = content;

            return response;
        }

        public static void WriteContent(Stream stream, HttpContent content, TransportContext context)
        {
            var serializer = JsonSerializer.CreateDefault();

            using (var sw = new StreamWriter(stream))
            using (var jw = new JsonTextWriter(sw))
            {
                jw.WriteStartArray();
                foreach (var id in Enumerable.Range(1, 100000))
                {
                    serializer.Serialize(jw, new TestModel()
                    {
                        Alias = "rvhuang",
                        BirthDate = new DateTime(1985, 02, 13),
                        FirstName = "Robert",
                        LastName = "Huang",
                        ID = id.ToString(),
                        MiddleName = "Vandenberg",
                    });
                    //sw.Flush();
                }
                jw.WriteEndArray();
            }
        }

        [Route("numbers")]
        [HttpGet]
        public HttpResponseMessage StreamData()
        {
            List<IntModel> numbers = GenerateNumbers();

            List<IntModel> numbersHold = new List<IntModel>();

            var response = Request.CreateResponse(HttpStatusCode.OK);
            response.Headers.TransferEncodingChunked = true;

            response.Content = new PushStreamContent((stream, HttpContent, context) =>
            {
                try
                {
                    foreach (var num in numbers)
                    {
                        Newtonsoft.Json.JsonSerializer serializer = new Newtonsoft.Json.JsonSerializer();
                        using (var writer = new StreamWriter(stream))
                        {
                            // problem here is that we are flushing a single IntModel but the angular call is setup to receive an array of IntModel
                            // don't know how to modify this
                            numbersHold.Clear();
                            numbersHold.Add(num);
                            serializer.Serialize(writer, numbersHold.ToArray());
                            stream.Flush();
                        }
                    }
                }
                finally
                {
                    stream.Close();
                }

            }, "application/json");

            response.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            return response;
        }

        protected List<IntModel> GenerateNumbers()
        {
            List<IntModel> ln = new List<IntModel>();

            for (int i = 0; i < 2; ++i)
            {
                var item = new IntModel();
                item.ID = i.ToString();
                ln.Add(item);
            }

            return ln;
        }
        #endregion

    }
}