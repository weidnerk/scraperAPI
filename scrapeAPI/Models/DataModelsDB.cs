using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Data.Entity.Validation;
using System.Linq;
using System.Web;

namespace scrapeAPI.Models
{
    public class DataModelsDB : DbContext
    {
        static DataModelsDB()
        {
            //do not try to create a database 
            Database.SetInitializer<DataModelsDB>(null);
        }

        public DataModelsDB()
            : base("name=OPWContext")
        {
        }

        public DbSet<OrderHistory> OrderHistory { get; set; }

        public string OrderHistorySave(List<OrderHistory> oh, int rptNumber, bool listingEnded)
        {
            string ret = string.Empty;
            try
            {
                foreach (OrderHistory item in oh)
                {
                    item.RptNumber = rptNumber;
                    item.ListingEnded = listingEnded;
                    OrderHistory.Add(item);
                }
                this.SaveChanges();
                //ret = oh.ID.ToString();
            }
            catch (DbEntityValidationException e)
            {
                foreach (var eve in e.EntityValidationErrors)
                {
                    ret = string.Format("Entity of type \"{0}\" in state \"{1}\" has the following validation errors:\n", eve.Entry.Entity.GetType().Name, eve.Entry.State);
                    foreach (var ve in eve.ValidationErrors)
                    {
                        ret += string.Format("- Property: \"{0}\", Error: \"{1}\"\n", ve.PropertyName, ve.ErrorMessage);
                    }
                }
            }
            catch (Exception exc)
            {
                ret = exc.Message;
            }
            return ret;
        }

    }
}