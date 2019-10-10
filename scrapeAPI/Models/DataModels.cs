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

    /// <summary>
    /// Used for prior scan.
    /// </summary>
    public class ModelViewTimesSold
    {
        public List<TimesSold> TimesSoldRpt { get; set; }
        public int ListingsProcessed { get; set; }
        public int TotalOrders { get; set; }
        // public int MatchedListings { get; set; }
        public int ItemCount { get; set; }
    }

    public class TimesSold
    {
        public string Title { get; set; }
        public int SoldQty { get; set; }
        public string EbayUrl { get; set; }
        public int RptNumber { get; set; }
        public string ImageUrl { get; set; }
        public decimal SellerPrice { get; set; }
        public DateTime? LatestSold { get; set; }
        public string ItemId { get; set; }
        public DateTime? Listed { get; set; }
        public string SellingState { get; set; }
        public string ListingStatus { get; set; }
        public decimal? ListingPrice { get; set; }
        public bool? IsMultiVariationListing { get; set; }
        public string ShippingServiceName { get; set; }
        public string ShippingServiceCost { get; set; }
    }

    /// <summary>
    /// Used when doing scan.
    /// </summary>
    public class ModelView
    {
        public List<Listing> Listings { get; set; }
        // public int MatchedListings { get; set; }
        public int TotalOrders { get; set; }
        public double ElapsedSeconds { get; set; }
        public int PercentTotalItemsProcesssed { get; set; }
        public int ReportNumber { get; set; }
        public int ItemCount { get; set; }
    }

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