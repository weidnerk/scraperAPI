/*
 * This file is heavily dependent on the .NET SDK (References/eBay.Service)
 * but different in that calls go through the eBayAPIInterfaceService object.
 * 
 * 
 */
using dsmodels;
using eBay.Service.Core.Soap;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Services.Protocols;

namespace scrapeAPI
{
    public class eBayItem
    {
        /// <summary>
        /// Verify whether item is ready to be added to eBay.
        /// 
        /// Return listedItemID, output error 
        /// My presets are: 
        ///     NEW condition 
        ///     BuyItNow fixed price
        ///     30 day duration
        ///     14-day returns w/ 20% restocking fee
        ///     payment method=PayPal
        ///     FREE shipping
        ///     buyer pays for return shipping
        /// </summary>
        public static string VerifyAddItemRequest(UserSettingsView settings,
            string title, 
            string description, 
            string categoryID, 
            double price, 
            List<string> pictureURLs, 
            ref List<string> errors, 
            int qtyToList,
            Listing listing)
        {
            //errors = null;
            string listedItemID = null;
            try
            {
                eBayAPIInterfaceService service = EbayCalls.eBayServiceCall(settings, "VerifyAddItem");

                VerifyAddItemRequestType request = new VerifyAddItemRequestType();
                request.Version = "949";
                request.ErrorLanguage = "en_US";
                request.WarningLevel = WarningLevelCodeType.High;

                var item = new ItemType();

                item.Title = title;
                item.Description = description;
                item.PrimaryCategory = new CategoryType
                {
                    CategoryID = categoryID
                };
                item.StartPrice = new AmountType
                {
                    Value = price,
                    currencyID = CurrencyCodeType.USD
                };

                // To view ConditionIDs follow the URL
                // http://developer.ebay.com/devzone/guides/ebayfeatures/Development/Desc-ItemCondition.html#HelpingSellersChoosetheRightCondition
                item.ConditionID = 1000;    // new
                item.Country = CountryCodeType.US;
                item.Currency = CurrencyCodeType.USD;
                // item.DispatchTimeMax = 2;       // pretty sure this is handling time

                // https://developer.ebay.com/devzone/xml/docs/reference/ebay/types/ListingDurationCodeType.html
                item.ListingDuration = "Days_30";
                item.ListingDuration = "GTC";

                // Buy It Now fixed price
                item.ListingType = ListingTypeCodeType.FixedPriceItem;
                // Auction
                //item.ListingType = ListingTypeCodeType.Chinese; 

                /*
                item.PaymentMethods = new BuyerPaymentMethodCodeTypeCollection
                {
                    BuyerPaymentMethodCodeType.PayPal
                };
                item.AutoPay = true;    // require immediate payment
                                        // Default testing paypal email address
                item.PayPalEmailAddress = "ventures2019@gmail.com";
                */

                item.PictureDetails = new PictureDetailsType();
                item.PictureDetails.PictureURL = new StringCollection();
                item.PictureDetails.PictureURL.AddRange(pictureURLs.ToArray());
                item.PostalCode = "33772";
                item.Quantity = qtyToList;

                item.ItemSpecifics = new NameValueListTypeCollection();

                NameValueListTypeCollection ItemSpecs = new NameValueListTypeCollection();

                var nv1 = new eBay.Service.Core.Soap.NameValueListType();
                var nv2 = new eBay.Service.Core.Soap.NameValueListType();
                StringCollection valueCol1 = new StringCollection();
                StringCollection valueCol2 = new StringCollection();

                if (!ItemSpecificExists(listing.ItemSpecifics, "Brand")) { 
                    nv1.Name = "Brand";
                    valueCol1.Add("Unbranded");
                    nv1.Value = valueCol1;
                    ItemSpecs.Add(nv1);
                }
                if (!ItemSpecificExists(listing.ItemSpecifics, "MPN"))
                {
                    nv2.Name = "MPN";
                    valueCol2.Add("Does Not Apply");
                    nv2.Value = valueCol2;
                    ItemSpecs.Add(nv2);
                }

                var revisedItemSpecs = ModifyItemSpecific(listing.ItemSpecifics);
                foreach (var i in revisedItemSpecs)
                {
                    var n = AddItemSpecifics(i);
                    ItemSpecs.Add(n);
                }

                item.ItemSpecifics = ItemSpecs;

                var pd = new ProductListingDetailsType();
                //var brand = new BrandMPNType();
                //brand.Brand = "Unbranded";
                //brand.MPN = unavailable;
                //pd.BrandMPN = brand;
                pd.UPC = "Does not apply";
                item.ProductListingDetails = pd;

                string returnDescr = "Please return if unstatisfied.";
                // returnDescr = "30 day returns. Buyer pays for return shipping";
                var sp = new SellerProfilesType();

                var spp = new SellerPaymentProfileType();
                spp.PaymentProfileName = "default";

                var srp = new SellerReturnProfileType();
                srp.ReturnProfileName = "mw";

                var ssp = new SellerShippingProfileType();
                ssp.ShippingProfileName = "mw";

                sp.SellerPaymentProfile = spp;
                sp.SellerReturnProfile = srp;
                sp.SellerShippingProfile = ssp;
                item.SellerProfiles = sp;
                // item.SellerProfiles.SellerPaymentProfile = spp;
                // item.SellerProfiles.SellerReturnProfile = srp;
                // item.SellerProfiles.SellerShippingProfile = ssp;

                /*
                item.ReturnPolicy = new ReturnPolicyType
                {
                    ReturnsAcceptedOption = "ReturnsAccepted",
                    ReturnsWithinOption = "Days_30",
                    //RefundOption = "MoneyBack",
                    //Description = returnDescr,
                    ShippingCostPaidByOption = "Seller"
                    //,
                    //RestockingFeeValue = "Percent_20",
                    //RestockingFeeValueOption = "Percent_20"
                };
                item.ShippingDetails = GetShippingDetail();
                */
                // item.DispatchTimeMax = 3;   // aka handling time

                item.Site = SiteCodeType.US;

                request.Item = item;

                VerifyAddItemResponseType response = service.VerifyAddItem(request);
                Console.WriteLine("ItemID: {0}", response.ItemID);

                // If item is verified, the item will be added.
                if (response.ItemID == "0")
                {
                    Console.WriteLine("=====================================");
                    Console.WriteLine("Add Item Verified");
                    Console.WriteLine("=====================================");
                    listedItemID = AddItemRequest(settings, item, ref errors);
                }
                else
                {
                    foreach (ErrorType e in response.Errors)
                    {
                        errors.Add(e.LongMessage);
                    }
                }
                return listedItemID;
            }
            catch (SoapException exc)
            {
                string s = exc.Message; 
                errors.Add(exc.Detail.InnerText);
                return null;
            }
            catch (Exception exc)
            {
                string s = exc.Message;
                errors.Add(s);
                return null;
            }
        }

        protected static eBay.Service.Core.Soap.NameValueListType AddItemSpecifics(ItemSpecific item)
        {
            var itemSpecs = new NameValueListTypeCollection();
            var nv2 = new eBay.Service.Core.Soap.NameValueListType();
            StringCollection valueCol2 = new StringCollection();

            nv2.Name = item.ItemName;
            valueCol2.Add(item.ItemValue);
            nv2.Value = valueCol2;

            return nv2;
        }
        protected static List<ItemSpecific> ModifyItemSpecific(List<ItemSpecific> itemSpecifics)
        {
            var specifics = new List<ItemSpecific>();
            foreach(var s in itemSpecifics)
            {
                if (!OmitSpecific(s.ItemName))
                {
                    specifics.Add(s);
                }
            }
            return specifics;
        }
        protected static bool ItemSpecificExists(List<ItemSpecific> itemSpecifics, string itemName)
        {
            foreach (var s in itemSpecifics)
            {
                if (s.ItemName == itemName)
                {
                    return true;
                }
            }
            return false;
        }
        protected static bool OmitSpecific(string name)
        {
            if (name == "Restocking Fee")
                return true;
            if (name == "All returns accepted")
                return true;
            if (name == "Item must be returned within")
                return true;
            if (name == "Refund will be given as")
                return true;
            if (name == "Return shipping will be paid by")
                return true;
            if (name == "Return shipping will be paid by")
                return true;

            return false;
        }
        protected static ShippingDetailsType GetShippingDetail()
        {
            ShippingDetailsType sd = new ShippingDetailsType();
            
            //sd.ApplyShippingDiscount = true;
            //sd.PaymentInstructions = "eBay .Net SDK test instruction.";
            //sd.ShippingRateType = ShippingRateTypeCodeType.StandardList;

            //adding domestic shipping

            ShippingServiceOptionsType domesticShipping1 = new ShippingServiceOptionsType();

            // see my notes in google doc
            domesticShipping1.ShippingService = ShippingServiceCodeType.ShippingMethodStandard.ToString();    // displays as "Standard Shipping" but for my account FAST 'N FREE
            //domesticShipping1.ShippingService = ShippingServiceCodeType.Other.ToString();                       // displays as "Economy Shipping" (slower shipping time)

            domesticShipping1.ShippingServiceCost = new AmountType { Value = 0, currencyID = CurrencyCodeType.USD };
            domesticShipping1.ShippingInsuranceCost = new AmountType { Value = 0, currencyID = CurrencyCodeType.USD };
            domesticShipping1.ShippingServicePriority = 4;
            domesticShipping1.LocalPickup = false;
            domesticShipping1.FreeShipping = true;

            // var s = new DispatchTimeMaxDetailsType();
            // s.DispatchTimeMax = 3;

            sd.ShippingServiceOptions = new ShippingServiceOptionsTypeCollection(new[] { domesticShipping1 });
            sd.ShippingType = ShippingTypeCodeType.Flat;

            return sd;
        }

        /// <summary>
        /// Add item to eBay. Once verified.
        /// </summary>
        /// <param name="item">Accepts ItemType object from VerifyAddItem method.</param>
        public static string AddItemRequest(UserSettingsView settings, ItemType item, ref List<string> errors)
        {
            eBayAPIInterfaceService service = EbayCalls.eBayServiceCall(settings, "AddItem");

            AddItemRequestType request = new AddItemRequestType();
            request.Version = "949";
            request.ErrorLanguage = "en_US";
            request.WarningLevel = WarningLevelCodeType.High;
            request.Item = item;

            AddItemResponseType response = service.AddItem(request);
            foreach (ErrorType e in response.Errors)
            {
                errors.Add(e.LongMessage);
            }

            Console.WriteLine("Item Added");
            Console.WriteLine("ItemID: {0}", response.ItemID); // Item ID
            return response.ItemID;
        }

        /// <summary>
        /// Retrieve item details.
        /// </summary>
        /// <param name="ItemID">eBay Item ID</param>
        public static void GetItemRequest(UserSettingsView settings, string ItemID)
        {
            eBayAPIInterfaceService service = EbayCalls.eBayServiceCall(settings, "GetItem");

            GetItemRequestType request = new GetItemRequestType();
            request.Version = "949";
            request.ItemID = ItemID;
            GetItemResponseType response = service.GetItem(request);

            Console.WriteLine("=====================================");
            Console.WriteLine("Item Iitle - {0}", response.Item.Title);
            Console.WriteLine("=====================================");

            Console.WriteLine("ItemID: {0}", response.Item.ItemID);
            Console.WriteLine("Primary Category: {0}", response.Item.PrimaryCategory.CategoryName);
            Console.WriteLine("Listing Duration: {0}", response.Item.ListingDuration);
            Console.WriteLine("Start Price: {0} {1}", response.Item.StartPrice.Value, response.Item.Currency);
            Console.WriteLine("Payment Type[0]: {0}", response.Item.PaymentMethods[0]);
            Console.WriteLine("PayPal Email Address: {0}", response.Item.PayPalEmailAddress);
            Console.WriteLine("Postal Code: {0}", response.Item.PostalCode);
            // ...Convert response object to JSON to see all
        }

    }
}