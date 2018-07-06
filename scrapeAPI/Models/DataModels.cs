using scrapeAPI.com.ebay.developer;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Web;

namespace scrapeAPI.Models
{
    [Table("SearchHistory")]
    public class SearchHistory
    {
        public int Id { get; set; }
        public string UserId { get; set; }
        public int ReportNumber { get; set; }
        public string Seller { get; set; }
        public int DaysBack { get; set; }
        public int MinSoldFilter { get; set; }

    }

    [Table("UserProfile")]
    public class UserProfile
    {
        public string Id { get; set; }
        public string AppID { get; set; }
        public string DevID { get; set; }
        public string CertID { get; set; }
        public string UserToken { get; set; }
        public string Firstname { get; set; }
        public string Lastname { get; set; }
    }
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
    }

    [Table("OrderHistory")]
    public class OrderHistory
    {
        public int ID { get; set; }
        public string Title { get; set; }
        public string SupplierPrice { get; set; }
        public string Qty { get; set; }
        //public string DateOfPurchaseStr { get; set; }
        public DateTime? DateOfPurchase { get; set; }
        public int RptNumber { get; set; }
        public string EbayUrl { get; set; }

        public string ImageUrl { get; set; }
        public bool ListingEnded { get; set; }
        public int PageNumber { get; set; }
        public string ItemId { get; set; }
    }
    public class ModelView
    {
        public List<Listing> Listings { get; set; }
        public int MatchedListings { get; set; }
        public int TotalOrders { get; set; }
        public double ElapsedSeconds { get; set; }
        public int PercentTotalItemsProcesssed { get; set; }
    }

    // Listing is used for the research reporting.
    // SingleItem is used for the detail page.
    // Case can be made to just use the Listing class.
    [Table("Listing")]
    public class Listing
    {
        [Key]
        public string ItemId { get; set; }
        public string Title { get; set; }
        public string ListingTitle { get; set; }
        public List<OrderHistory> Orders { get; set; }
        public string EbayUrl { get; set; }
        public string Description { get; set; }
        public decimal SupplierPrice { get; set; }
        public string PictureUrl { get;set; }   // store picture urls as a semi-colon delimited string
        public decimal ListingPrice { get; set; }
        public string Source { get; set; }
        public string PrimaryCategoryID { get; set; }
        public string PrimaryCategoryName { get; set; }
        public byte SourceId { get; set; }
    }

    [Table("vwPriceCompare")]
    public class ImageCompare
    {
        public string SourceItemNo { get; set; }
        public string SourceImgUrl { get; set; }
        public string EbayImgUrl { get; set; }
        public int CategoryId { get; set; }
        [Key]
        [Column(Order = 1)]
        public DateTime DateOfPurchase { get; set; }
        public int EbayImgCount { get; set; }
        public string PictureUrl { get; set; }
        public string EbayUrl { get; set; }
        [Key]
        [Column(Order = 2)]
        public string EbayItemId { get; set; }
        public string SourceUrl { get; set; }
        public string SourceTitle { get; set; }
        public string EbayTitle { get; set; }
        public string EbaySeller { get; set; }
        public string SoldQty { get; set; }
        public string Limit { get; set; }
        public string Availability { get; set; }
        public decimal SourcePrice { get; set; }
        public decimal EbaySellerPrice { get; set; }
        public string SourceRating { get; set; }
        public string PrimaryCategoryName { get; set; }
    }

    public class IntModel
    {
        public string ID
        {
            get; set;
        }
    }

    public class TestModel
    {
        public string FirstName
        {
            get; set;
        }

        public string MiddleName
        {
            get; set;
        }

        public DateTime BirthDate
        {
            get; set;
        }

        public string LastName
        {
            get; set;
        }

        public string Alias
        {
            get; set;
        }

        public string ID
        {
            get; set;
        }
    }
    public class SearchItemCustom
    {
        public SearchItem searchItem { get; set; }
        public int PageNumber { get; set; }
    }
}