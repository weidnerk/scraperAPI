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
    public class DataModelsDB
    {
        //dsmodels.Repository db = new dsmodels.Repository();
        private IRepository _repository;

        private ApplicationUserManager _userManager;
        public DataModelsDB(IRepository repository)
        {
            _repository = repository;
        }
        public ApplicationUserManager UserManager
        {
            get => _userManager ?? HttpContext.Current.GetOwinContext().GetUserManager<ApplicationUserManager>();
            private set
            {
                _userManager = value;
            }
        }

        public UserProfileView UserProfileGet(ApplicationUser usr)
        {
            var profile = _repository.GetUserProfileView(usr.Id);
            return profile;
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