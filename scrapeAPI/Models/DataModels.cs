using scrapeAPI.com.ebay.developer;
using System;
using System.Collections.Generic;
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
        public string Url { get; set; }
        public int RptNumber { get; set; }
        public string ImageUrl { get; set; }
        public decimal Price { get; set; }
        public DateTime? EarliestSold { get; set; }
        public string ItemId { get; set; }
    }

    [Table("OrderHistory")]
    public class OrderHistory
    {
        public int ID { get; set; }
        public string Title { get; set; }
        public string Price { get; set; }
        public string Qty { get; set; }
        //public string DateOfPurchaseStr { get; set; }
        public DateTime? DateOfPurchase { get; set; }
        public int RptNumber { get; set; }
        public string Url { get; set; }

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

    public class Listing
    {
        public string Title { get; set; }
        public List<OrderHistory> Orders { get; set; }
        public string Url { get; set; }

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