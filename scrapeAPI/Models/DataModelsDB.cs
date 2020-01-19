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

        //public DbSet<SearchHistoryView> SearchHistoryView { get; set; }
        private ApplicationUserManager _userManager;

        public ApplicationUserManager UserManager
        {
            get => _userManager ?? HttpContext.Current.GetOwinContext().GetUserManager<ApplicationUserManager>();
            private set
            {
                _userManager = value;
            }
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