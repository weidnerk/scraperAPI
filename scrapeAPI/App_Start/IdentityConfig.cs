using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNet.Identity;
using Microsoft.AspNet.Identity.EntityFramework;
using Microsoft.AspNet.Identity.Owin;
using Microsoft.Owin;
using scrapeAPI.Models;

namespace scrapeAPI
{
    // Configure the application user manager used in this application. UserManager is defined in ASP.NET Identity and is used by the application.

    public class ApplicationUserManager : UserManager<ApplicationUser>
    {
        public ApplicationUserManager(IUserStore<ApplicationUser> store)
            : base(store)
        {
        }

        public static ApplicationUserManager Create(IdentityFactoryOptions<ApplicationUserManager> options, IOwinContext context)
        {
            var manager = new ApplicationUserManager(new UserStore<ApplicationUser>(context.Get<ApplicationDbContext>()));
            // Configure validation logic for usernames
            manager.UserValidator = new UserValidator<ApplicationUser>(manager)
            {
                AllowOnlyAlphanumericUserNames = false,
                RequireUniqueEmail = true
            };
            // Configure validation logic for passwords
            manager.PasswordValidator = new PasswordValidator
            {
                RequiredLength = 8,
                RequireNonLetterOrDigit = false,
                RequireDigit = false,
                RequireLowercase = false,
                RequireUppercase = false,
            };
            manager.EmailService = new EmailService();
            var dataProtectionProvider = options.DataProtectionProvider;
            if (dataProtectionProvider != null)
            {
                manager.UserTokenProvider = new DataProtectorTokenProvider<ApplicationUser>(dataProtectionProvider.Create("ASP.NET Identity"));
            }
            return manager;
        }
    }

    // Configure the application user manager used in this application. UserManager is defined in ASP.NET Identity and is used by the application.
    public class EmailService : IIdentityMessageService
    {
        public Task SendAsync(IdentityMessage message)
        {
            const string apiKey = "key-ef7a2525b9a4141408b40cd4d4e438e0";
            const string sandBox = "sandbox5c2ed57ac7b94f0ea5d372f3194b026c.mailgun.org";
            byte[] apiKeyAuth = Encoding.ASCII.GetBytes($"api:{apiKey}");
            var httpClient = new HttpClient { BaseAddress = new Uri("https://api.mailgun.net/v3/") };
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(apiKeyAuth));

            var form = new Dictionary<string, string>
            {
                ["from"] = "postmaster@sandbox5c2ed57ac7b94f0ea5d372f3194b026c.mailgun.org",
                ["to"] = message.Destination,
                ["subject"] = message.Subject,
                ["text"] = message.Body
            };

            HttpResponseMessage response = httpClient.PostAsync(sandBox + "/messages", new FormUrlEncodedContent(form)).Result;
            return Task.FromResult((int)response.StatusCode);
        }
    }
}
