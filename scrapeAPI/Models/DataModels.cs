using dsmodels;
using scrapeAPI.com.ebay.developer;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Web;

namespace scrapeAPI.Models
{

    public class UserProfileVM
    {
        public string Id { get; set; }
        public string userName { get; set; }
        public string AppID { get; set; }
        public string DevID { get; set; }
        public string CertID { get; set; }
        public string UserToken { get; set; }
        public string Firstname { get; set; }
        public string Lastname { get; set; }
        public string ApplicationID { get; set; }
    }

    public class ModelViewTimesSold
    {
        public List<TimesSold> TimesSoldRpt { get; set; }
        public int ListingsProcessed { get; set; }
        public int TotalOrders { get; set; }
        public int MatchedListings { get; set; }
    }

    public class TimesSold
    {
        public string Title { get; set; }
        public int SoldQty { get; set; }
        public string EbayUrl { get; set; }
        public int RptNumber { get; set; }
        public string ImageUrl { get; set; }
        public decimal SupplierPrice { get; set; }
        public DateTime? EarliestSold { get; set; }
        public string ItemId { get; set; }
        public DateTime? Listed { get; set; }
        public string SellingState { get; set; }
        public string ListingStatus { get; set; }
    }

    public class ModelView
    {
        public List<ListingX> Listings { get; set; }
        public int MatchedListings { get; set; }
        public int TotalOrders { get; set; }
        public double ElapsedSeconds { get; set; }
        public int PercentTotalItemsProcesssed { get; set; }
        public int ReportNumber { get; set; }
        public int ItemCount { get; set; }
    }

    // Listing is used for the research reporting.
    // SingleItem is used for the detail page.
    // Case can be made to just use the Listing class.
    //[Table("Listing")]
    //public class Listing
    //{
    //    [Key]
    //    public string ItemId { get; set; }
    //    public string Title { get; set; }
    //    public string ListingTitle { get; set; }
    //    public List<OrderHistory> Orders { get; set; }
    //    public string EbayUrl { get; set; }
    //    public string Description { get; set; }
    //    public decimal SupplierPrice { get; set; }
    //    public string PictureUrl { get; set; }   // store picture urls as a semi-colon delimited string
    //    public decimal ListingPrice { get; set; }
    //    public string Source { get; set; }
    //    public string PrimaryCategoryID { get; set; }
    //    public string PrimaryCategoryName { get; set; }
    //    public byte SourceId { get; set; }
    //    public int Qty { get; set; }
    //    public string ListingStatus { get; set; }

    //}

    //[Table("PostedListings")]
    //public class PostedListing
    //{
    //    public string EbaySeller { get; set; }
    //    [Key]
    //    public string EbayItemID { get; set; }
    //    public string EbayUrl { get; set; }
    //    public int SourceID { get; set; }
    //    public string SupplierItemID { get; set; }
    //    public string SourceUrl { get; set; }
    //    public decimal SupplierPrice { get; set; }
    //    public string Title { get; set; }
    //    public decimal Price { get; set; }
    //    public string Description { get; set; }
    //    public string Pictures { get; set; }
    //    public int CategoryID { get; set; }
    //    public string PrimaryCategoryID { get; set; }
    //    public string PrimaryCategoryName { get; set; }
    //    public string ListedItemID { get; set; }
    //    public DateTime? Listed { get; set; }
    //    public DateTime? Removed { get; set; }
    //    public byte ListedQty { get; set; }
    //}

    // How is key defined?
    // Can have repeating ebayitemid with different prices
    // and can have same ebay item mapped to multiple sams's items
    //[Table("vwPriceCompare")]
    //public class ImageCompare
    //{
    //    [Key]
    //    [Column(Order = 2)]
    //    public string SourceItemNo { get; set; }
    //    public string SourceImgUrl { get; set; }
    //    public string EbayImgUrl { get; set; }
    //    [Key]
    //    [Column(Order = 1)]
    //    public int CategoryId { get; set; }
    //    //public DateTime DateOfPurchase { get; set; }
    //    public int EbayImgCount { get; set; }
    //    public string PictureUrl { get; set; }
    //    public string EbayUrl { get; set; }
    //    [Key]
    //    [Column(Order = 3)]
    //    public string EbayItemId { get; set; }
    //    public string SourceUrl { get; set; }
    //    public string SourceTitle { get; set; }
    //    public string EbayTitle { get; set; }
    //    public string EbaySeller { get; set; }
    //    public int SoldQty { get; set; }
    //    public string Limit { get; set; }
    //    public string Availability { get; set; }
    //    public decimal SourcePrice { get; set; }
    //    [Key]
    //    [Column(Order = 4)]
    //    public decimal EbaySellerPrice { get; set; }
    //    public string SourceRating { get; set; }
    //    public string PrimaryCategoryID { get; set; }
    //    public string PrimaryCategoryName { get; set; }
    //    public long FeedbackScore { get; set; }
    //    public string SourceDescription { get; set; }
    //    public string EbayDescription { get; set; }
    //    [Key]
    //    [Column(Order = 5)]
    //    public decimal ShippingAmount { get; set; }
    //    public DateTime? PostedListingCreated { get; set; }
    //    public DateTime? Listed { get; set; }
    //    public DateTime? Removed { get; set; }
    //    public decimal MinPrice { get; set; }               // need to sell for at least this to break even
    //    public decimal CostPlusTax { get; set; }
    //}

    public class SearchReport
    {
        public int? PostedListingID { get; set; }    // from PostedListings
        [Key]
        [Column(Order = 2)]
        public string SourceItemNo { get; set; }
        public string SourceImgUrl { get; set; }
        public string EbayImgUrl { get; set; }
        [Key]
        [Column(Order = 1)]
        public int CategoryId { get; set; }
        //public DateTime DateOfPurchase { get; set; }
        public int EbayImgCount { get; set; }
        public string PictureUrl { get; set; }
        public string EbayUrl { get; set; }
        [Key]
        [Column(Order = 3)]
        public string EbayItemId { get; set; }
        public string SourceUrl { get; set; }
        public string SourceTitle { get; set; }
        public string EbayTitle { get; set; }
        public string EbaySeller { get; set; }
        public int SoldQty { get; set; }
        public string Limit { get; set; }
        public string Availability { get; set; }
        public decimal SourcePrice { get; set; }
        [Key]
        [Column(Order = 4)]
        public decimal EbaySellerPrice { get; set; }
        public string SourceRating { get; set; }
        public string PrimaryCategoryID { get; set; }
        public string PrimaryCategoryName { get; set; }
        public long FeedbackScore { get; set; }
        public string SourceDescription { get; set; }
        public string EbayDescription { get; set; }
        [Key]
        [Column(Order = 5)]
        public decimal ShippingAmount { get; set; }
        public DateTime? PostedListingCreated { get; set; }
        public DateTime? Listed { get; set; }
        public DateTime? Removed { get; set; }
        public decimal MinPrice { get; set; }               // need to sell for at least this to break even
        public decimal CostPlusTax { get; set; }
        public byte? Qty { get; set; }
        public DateTime? DatePurchased { get; set; }
        public string ListedItemID { get; set; }
    }

    public class SearchItemCustom
    {
        public SearchItem searchItem { get; set; }
        public int PageNumber { get; set; }
    }

}