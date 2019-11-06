using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using dsmodels;
using eBay.Service.Core.Soap;

namespace scrapeAPI
{
    public static class StoreCheck
    {
        static DataModelsDB db = new DataModelsDB();
        /// <summary>
        /// does listing id exist in the db?
        /// </summary>
        /// <param name="itemid"></param>
        /// <returns></returns>
        public static bool LookupItemid(string itemid)
        {
            var result = db.Listings.Where(x => x.ListedItemID == itemid).ToList();
            if (result.Count == 0) return false;
            return true;
        }

        /// <summary>
        /// items are listed that are not in the db
        /// </summary>
        /// <param name="totalListed"></param>
        /// <returns></returns>
        public static List<string> DBIsMissingItems(UserSettingsView settings, ref ItemTypeCollection storeItems)
        {
            var items = new List<string>();
            int cnt = 0;
            if (storeItems.Count == 0)
            {
                storeItems = ebayAPIs.GetSellerList(settings, out string errMsg);
            }
            if (storeItems != null)
            {
                // scan each item in store - is it in db?
                foreach (ItemType oItem in storeItems)
                {
                    bool r = scrapeAPI.StoreCheck.LookupItemid(oItem.ItemID);
                    if (!r)
                    {
                        items.Add(oItem.Title);
                        Console.WriteLine(oItem.ItemID);
                        Console.WriteLine(oItem.Title);
                        Console.WriteLine();
                        ++cnt;
                    }
                }
            }
            return items;
        }

    }
}