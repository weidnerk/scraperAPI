using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Data.Entity.Core.Objects;
using System.Data.Entity.Infrastructure;
using System.Data.Entity.Validation;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using dsmodels;
using Microsoft.AspNet.Identity.Owin;
using scrapeAPI.Controllers;

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
        public DbSet<UserProfile> UserProfiles { get; set; }
        public DbSet<SearchHistory> SearchHistory { get; set; }
        public DbSet<Listing> Listings { get; set; }
        public DbSet<PostedListing> PostedListings { get; set; }
        //public DbSet<ImageCompare> ItemImages { get; set; }
        public DbSet<SearchReport> SearchResults { get; set; }
        public DbSet<SourceCategories> SourceCategories { get; set; }

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
            List<SearchReport> data =
                Database.SqlQuery<SearchReport>(
                "select * from dbo.fnPriceCompare(@categoryId)",
                new SqlParameter("@categoryId", categoryId))
            .ToList();
            return data;
        }

        public async Task<Listing> GetListing(string itemId)
        {
            var found = await this.Listings.FirstOrDefaultAsync(r => r.ItemId == itemId);
            return found;
        }

        public async Task ListingSave(Listing listing)
        {
            try
            {
                var found = await this.Listings.FirstOrDefaultAsync(r => r.ItemId == listing.ItemId);
                if (found == null)
                    Listings.Add(listing);
                else
                {
                    found.ListingPrice = listing.ListingPrice;
                    found.Source = listing.Source;
                    found.PictureUrl = listing.PictureUrl;
                    found.Title = listing.Title;
                    found.ListingTitle = listing.ListingTitle;
                    found.EbayUrl = listing.EbayUrl;
                    found.PrimaryCategoryID = listing.PrimaryCategoryID;
                    found.PrimaryCategoryName = listing.PrimaryCategoryName;
                    found.Description = listing.Description;
                    found.SourceId = listing.SourceId;
                    this.Entry(found).State = EntityState.Modified;
                }
                await this.SaveChangesAsync();
            }
            catch (Exception exc)
            {
                string msg = dsutil.DSUtil.ErrMsg("", exc);
            }
        }

        public async Task<Listing> ListingGet(string itemId)
        {
            try
            {
                var listing = await this.Listings.FirstOrDefaultAsync(r => r.ItemId == itemId);
                return listing;
            }
            catch (Exception exc)
            {
                string msg = dsutil.DSUtil.ErrMsg("", exc);
                return null;
            }
        }

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

        public async Task SearchHistorySave(SearchHistory sh)
        {
            try
            {
                SearchHistory.Add(sh);
                await this.SaveChangesAsync();
            }
            catch (Exception exc)
            {
            }
        }

        public async Task<UserProfile> UserProfileGet(ApplicationUser usr)
        {
            var profile = await this.UserProfiles.FindAsync(usr.Id);
            return profile;
        }

        // return error string
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

                var profile = this.UserProfiles.Find(user.Id);
                if (profile != null)
                {
                    profile.AppID = p.AppID;
                    profile.CertID = p.CertID;
                    profile.DevID = p.DevID;
                    profile.UserToken = p.UserToken;
                    Entry(profile).Property(x => x.Firstname).IsModified = false;
                    Entry(profile).Property(x => x.Lastname).IsModified = false;
                    this.Entry(profile).State = EntityState.Modified;
                }
                else
                {
                    var newprofile = new UserProfile();
                    newprofile.AppID = p.AppID;
                    newprofile.CertID = p.CertID;
                    newprofile.DevID = p.DevID;
                    newprofile.UserToken = p.UserToken;
                    newprofile.Id = user.Id;
                    newprofile.Firstname = p.Firstname;
                    newprofile.Lastname = p.Lastname;
                    UserProfiles.Add(newprofile);
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

        public async Task<bool> UpdateListedItemID(PostedListing listing, string listedItemID)
        {
            bool ret = false;
            var rec = await this.PostedListings.FirstOrDefaultAsync(r => r.SourceID == listing.SourceID && r.SupplierItemID == listing.SupplierItemID);
            if (rec != null)
            {
                ret = true;
                rec.ListedItemID = listedItemID;
                rec.Listed = listing.Listed;

                using (var context = new DataModelsDB())
                {
                    // Pass the entity to Entity Framework and mark it as modified
                    context.Entry(rec).State = EntityState.Modified;
                    context.SaveChanges();
                }
            }
            return ret;
        }

        public async Task<bool> UpdateRemovedDate(PostedListing listing)
        {
            bool ret = false;
            var rec = await this.PostedListings.FirstOrDefaultAsync(r => r.EbayItemID == listing.EbayItemID);
            if (rec != null)
            {
                ret = true;
                rec.Removed = DateTime.Now;

                using (var context = new DataModelsDB())
                {
                    // Pass the entity to Entity Framework and mark it as modified
                    context.Entry(rec).State = EntityState.Modified;
                    context.SaveChanges();
                }
            }
            return ret;
        }

    }
}