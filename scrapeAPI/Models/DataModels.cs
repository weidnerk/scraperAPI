using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Web;

namespace scrapeAPI.Models
{
    public class ModelViewTimesSold
    {
        public List<TimesSold> TimesSoldRpt { get; set; }
        public int NumItems { get; set; }
    }

    public class TimesSold
    {
        public string Title { get; set; }
        public int SoldQty { get; set; }
        public string Url { get; set; }
        public int RptNumber { get; set; }
        public string ImageUrl { get; set; }
        public string Price { get; set; }
        public DateTime? EarliestSold { get; set; }
    }

    [Table("OrderHistory")]
    public class OrderHistory
    {
        public int ID { get; set; }
        public string Title { get; set; }
        public string Price { get; set; }
        public string Qty { get; set; }
        public string DateOfPurchaseStr { get; set; }
        public DateTime? DateOfPurchase { get; set; }
        public int RptNumber { get; set; }
        public string Url { get; set; }

        public string ImageUrl { get; set; }
        public bool ListingEnded { get; set; }
    }
    public class ModelView
    {
        public List<Listing> Listing { get; set; }
        public int TotalItems { get; set; }
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

}