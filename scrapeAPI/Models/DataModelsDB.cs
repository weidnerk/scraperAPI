using dsmodels;
using Microsoft.AspNet.Identity.Owin;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Data.Entity.Validation;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;
using System.Web;

namespace scrapeAPI.Models
{
    public class DataModelsDB : DbContext
    {
        dsmodels.DataModelsDB db = new dsmodels.DataModelsDB();

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
        public DbSet<SellerOrderHistory> SellerOrderHistory { get; set; }
        public DbSet<SearchHistory> SearchHistory { get; set; }
        public DbSet<SearchReport> SearchResults { get; set; }
        private ApplicationUserManager _userManager;

        public ApplicationUserManager UserManager
        {
            get => _userManager ?? HttpContext.Current.GetOwinContext().GetUserManager<ApplicationUserManager>();
            private set
            {
                _userManager = value;
            }
        }

        public List<SearchReport> GetSearchReport(int categoryId)
        {
            int sourceId = db.SourceIDFromCategory(categoryId);
            if (sourceId == 2)
            {
                List<SearchReport> data =
                    Database.SqlQuery<SearchReport>(
                    "select * from dbo.fnWalPriceCompare(@categoryId)",
                    new SqlParameter("@categoryId", categoryId))
                .ToList();
                return data;
            }

            if (sourceId == 1)
            {
                List<SearchReport> data =
                Database.SqlQuery<SearchReport>(
                "select * from dbo.fnPriceCompare(@categoryId)",
                new SqlParameter("@categoryId", categoryId))
                .ToList();
                return data;
            }
            return null;
        }

        public IQueryable<TimesSold> GetScanData(int rptNumber, DateTime dateFrom, int storeID, string itemID)
        {
            var p = new SqlParameter();
            p.ParameterName = "itemID";
            if (!string.IsNullOrEmpty(itemID))
            {
                p.Value = itemID;
            }
            else
            {
                p.Value = DBNull.Value;
            }

            var data = Database.SqlQuery<TimesSold>(
                "exec sp_GetScanReport @rptNumber, @dateFrom, @storeID, @itemID",
                new SqlParameter("rptNumber", rptNumber),
                new SqlParameter("dateFrom", dateFrom),
                new SqlParameter("storeID", storeID),
                p
                ).AsQueryable();
            return data;
        }

        public UserProfile UserProfileGet(ApplicationUser usr, string appID)
        {
            if (!string.IsNullOrEmpty(appID))
            {
                var profile = db.GetUserProfile(usr.Id);
                return profile;
            }
            else
            {
                var profile = db.GetUserProfile(usr.Id);
                return profile;
            }
        }
        public UserSettingsView UserSettingsGet(ApplicationUser usr)
        {
            var userSettings = db.GetUserSettings(usr.Id);
            return userSettings;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="p"></param>
        /// <returns>return error string</returns>
        public async Task<string> UserProfileSaveAsync(UserProfileVM p)
        {
            string ret = string.Empty;
            try
            {
                var user = await UserManager.FindByNameAsync(p.userName);
                if (user == null)
                {
                    return "user does not exist";
                }
                var profile = db.GetUserProfile(user.Id);
                if (profile != null)
                {
                    //profile.AppID = p.AppID;
                    //profile.CertID = p.CertID;
                    //profile.DevID = p.DevID;
                    //profile.UserToken = p.UserToken;
                    Entry(profile).Property(x => x.Firstname).IsModified = false;
                    Entry(profile).Property(x => x.Lastname).IsModified = false;
                    this.Entry(profile).State = EntityState.Modified;
                }
                else
                {
                    var newprofile = new UserSettingsView();
                    newprofile.AppID = p.AppID;
                    newprofile.CertID = p.CertID;
                    newprofile.DevID = p.DevID;
                    newprofile.Token = p.UserToken;
                    newprofile.UserID = user.Id;
                    //newprofile.Firstname = p.Firstname;
                    //newprofile.Lastname = p.Lastname;
                    //db.UserProfilesView.Add(newprofile);
                }
                await this.SaveChangesAsync();
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

        public async Task<bool> GetEmailTaken(string email)
        {
            bool taken = true;
            try
            {
                // if user is null, then emailTaken = false
                var user = await UserManager.FindByNameAsync(email);
                taken = (user == null) ? false : true;
            }
            catch (Exception ex)
            {
                string s = ex.Message;
            }
            return taken;
        }
    }
}