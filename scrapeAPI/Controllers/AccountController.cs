﻿/*
 * 
 * say user forgets their password
 * email them a new password (reset their password), then they have option to change the password
 * 
 * 
 * TODO
 * 
 */

using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Threading.Tasks;
using System.Web;
using System.Web.Http;
using System.Web.Http.ModelBinding;
using dsmodels;
using Microsoft.AspNet.Identity;
using Microsoft.AspNet.Identity.EntityFramework;
using Microsoft.AspNet.Identity.Owin;
using Microsoft.Owin.Security;
using Microsoft.Owin.Security.Cookies;
using Microsoft.Owin.Security.OAuth;
using scrapeAPI.Models;
using scrapeAPI.Providers;
using Utility.Results;

namespace scrapeAPI.Controllers
{
    public class ResetPasswordViewModel
    {
        public string Email { get; set; }

        public string Code { get; set; }

        public string Password { get; set; }
    }

    [Authorize]
    [RoutePrefix("api/Account")]
    public class AccountController : ApiController
    {
        private scrapeAPI.Models.DataModelsDB db;
        private IRepository _repository;
        private const string LocalLoginProvider = "Local";
        private ApplicationUserManager _userManager;
        const string _logfile = "log.txt";

        public AccountController(IRepository repository)
        {
            _repository = repository;
            db = new scrapeAPI.Models.DataModelsDB(_repository);
        }

        public AccountController(ApplicationUserManager userManager,
            ISecureDataFormat<AuthenticationTicket> accessTokenFormat)
        {
            UserManager = userManager;
            AccessTokenFormat = accessTokenFormat;
        }

        public ApplicationUserManager UserManager
        {
            get
            {
                return _userManager ?? HttpContext.Current.GetOwinContext().GetUserManager<ApplicationUserManager>();
            }
            private set
            {
                _userManager = value;
            }
        }

        public ISecureDataFormat<AuthenticationTicket> AccessTokenFormat { get; private set; }

        // GET api/Account/UserInfo
        [HostAuthentication(DefaultAuthenticationTypes.ExternalBearer)]
        [Route("UserInfo")]
        public UserInfoViewModel GetUserInfo()
        {
            ExternalLoginData externalLogin = ExternalLoginData.FromIdentity(User.Identity as ClaimsIdentity);

            return new UserInfoViewModel
            {
                Email = User.Identity.GetUserName(),
                HasRegistered = externalLogin == null,
                LoginProvider = externalLogin != null ? externalLogin.LoginProvider : null
            };
        }

        // POST api/Account/Logout
        [Route("Logout")]
        public IHttpActionResult Logout()
        {
            Authentication.SignOut(CookieAuthenticationDefaults.AuthenticationType);
            return Ok();
        }

        // GET api/Account/ManageInfo?returnUrl=%2F&generateState=true
        [Route("ManageInfo")]
        public async Task<ManageInfoViewModel> GetManageInfo(string returnUrl, bool generateState = false)
        {
            IdentityUser user = await UserManager.FindByIdAsync(User.Identity.GetUserId());
            if (user == null)
            {
                return null;
            }

            List<UserLoginInfoViewModel> logins = new List<UserLoginInfoViewModel>();

            foreach (IdentityUserLogin linkedAccount in user.Logins)
            {
                logins.Add(new UserLoginInfoViewModel
                {
                    LoginProvider = linkedAccount.LoginProvider,
                    ProviderKey = linkedAccount.ProviderKey
                });
            }

            if (user.PasswordHash != null)
            {
                logins.Add(new UserLoginInfoViewModel
                {
                    LoginProvider = LocalLoginProvider,
                    ProviderKey = user.UserName,
                });
            }

            return new ManageInfoViewModel
            {
                LocalLoginProvider = LocalLoginProvider,
                Email = user.UserName,
                Logins = logins,
                ExternalLoginProviders = GetExternalLogins(returnUrl, generateState)
            };
        }

        [HttpGet]
        [Route("userprofileget")]
        public async Task<IHttpActionResult> UserProfileGet(string userName)
        {
            try
            {
                var user = await UserManager.FindByNameAsync(userName);
                var p = db.UserProfileGet(user);
                if (p == null)
                    return NotFound();
                else
                    return Ok(p);
            }
            catch (Exception exc)
            {
                string msg = dsutil.DSUtil.ErrMsg("UserProfileGet", exc);
                dsutil.DSUtil.WriteFile(_logfile, msg, userName);
                return Content(HttpStatusCode.InternalServerError, msg);
            }
        }

        [HttpGet]
        [Route("usersettingsviewget")]
        public async Task<IHttpActionResult> UserSettingsViewGet(string userName)
        {
            var user = await UserManager.FindByNameAsync(userName);
            string connStr = ConfigurationManager.ConnectionStrings["OPWContext"].ConnectionString;
            var settings = _repository.GetUserSettingsView(connStr, user.Id);
            // dsutil.DSUtil.WriteFile(_logfile, "UserSettingsGet ", user.UserName);
            if (settings == null)
                return NotFound();
            else
                return Ok(settings);
        }
        [HttpGet]
        [Route("usersettingsviewgetbystore")]
        public async Task<IHttpActionResult> UserSettingsViewGet(string userName, int storeID)
        {
            var user = await UserManager.FindByNameAsync(userName);
            string connStr = ConfigurationManager.ConnectionStrings["OPWContext"].ConnectionString;
            var settings = _repository.GetUserSettingsView(connStr, user.Id, storeID);
            // dsutil.DSUtil.WriteFile(_logfile, "UserSettingsGet ", user.UserName);
            if (settings == null)
                return NotFound();
            else
                return Ok(settings);
        }

        // POST api/Account/ChangePassword
        [HttpPost]
        [Route("changepassword")]
        public async Task<IHttpActionResult> ChangePassword(ChangePasswordBindingModel model)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest("The was a problem changing your password.  Please try again.");
            }
            IdentityResult result = await UserManager.ChangePasswordAsync(User.Identity.GetUserId(), model.OldPassword,
                model.NewPassword);
            
            if (!result.Succeeded)
            {
                return GetErrorResult(result);
            }

            return Ok();
        }

        [HttpPost]
        [AllowAnonymous]
        [Route("resetpassword")]
        public async Task<IHttpActionResult> ResetPassword(ResetPasswordViewModel model)
        {
            //if (!ModelState.IsValid)
            //{
            //    return View(model);
            //}
            var user = await UserManager.FindByNameAsync(model.Email);
            if (user == null)
            {
                // Don't reveal that the user does not exist
            }
            string code = await UserManager.GeneratePasswordResetTokenAsync(user.Id);
            model.Code = code;
            var result = await UserManager.ResetPasswordAsync(user.Id, model.Code, model.Password);
            if (result.Succeeded)
            {
                return Ok();
            }
            return BadRequest();
        }

        [HttpPost]
        [AllowAnonymous]
        [Route("setrandompassword")]
        public async Task<IHttpActionResult> SetRandomPassword(ForgotPasswordViewModel vm)
        {
            var user = await UserManager.FindByEmailAsync(vm.EmailAddress);
            try
            {
                if (user == null)
                {
                    // Don't reveal that the user does not exist
                    return BadRequest();
                }

                string code = await UserManager.GeneratePasswordResetTokenAsync(user.Id);
                string pwd = DateTime.Now.ToFileTimeUtc().ToString();
                var result = await UserManager.ResetPasswordAsync(user.Id, code, pwd);
                if (result.Succeeded)
                {
                    dsutil.DSUtil.SendMailDev(vm.EmailAddress, "OPW credentiuals", pwd);
                    // await dsutil.DSUtil.SendMailProd(vm.EmailAddress, "temp password is " + pwd, "OPW credentiuals", "localhost");
                    return Ok();
                }
                return BadRequest();
            }
            catch (Exception exc)
            {
                dsutil.DSUtil.WriteFile(_logfile, "SetRandomPassword " + exc.Message, user.UserName);
                return InternalServerError(exc);
            }
        }

        [HttpPost]
        [AllowAnonymous]
        [Route("ForgotPassword")]
        public async Task<IHttpActionResult> ForgotPassword(ForgotPasswordViewModel model)
        {
            if (ModelState.IsValid)
            {
                var user = await UserManager.FindByNameAsync(model.EmailAddress);

                if (user == null || !(await UserManager.IsEmailConfirmedAsync(user.Id)))
                {
                    return Ok();
                }

                try
                {
                    var code = await UserManager.GeneratePasswordResetTokenAsync(user.Id);
                    var callbackUrl = new Uri(@"http://onepluswonder/scraper/ConfirmEmail?userid=" + user.Id + "&code=" + code);

                    string subject = "Reset Password";
                    string body = "Please reset your password by clicking <a href=\"" + callbackUrl + "\">here</a>";
                    await UserManager.SendEmailAsync(user.Email, subject, body);
                }

                catch (Exception ex)
                {
                    throw new Exception(ex.ToString());
                }

                return Ok();
            }

            // If we got this far, something failed, redisplay form
            return BadRequest(ModelState);
        }

        public async Task<IHttpActionResult> ConfirmEmail(string userId, string code)
        {
            if (userId == null || code == null)
            {
                return BadRequest();
            }
            var result = await UserManager.ConfirmEmailAsync(userId, code);
            if (result.Succeeded)
            {
                return Ok();
            }
            return BadRequest();
        }

        // POST api/Account/SetPassword
        [Route("SetPassword")]
        public async Task<IHttpActionResult> SetPassword(SetPasswordBindingModel model)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            IdentityResult result = await UserManager.AddPasswordAsync(User.Identity.GetUserId(), model.NewPassword);

            if (!result.Succeeded)
            {
                return GetErrorResult(result);
            }

            return Ok();
        }

        // POST api/Account/AddExternalLogin
        [Route("AddExternalLogin")]
        public async Task<IHttpActionResult> AddExternalLogin(AddExternalLoginBindingModel model)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            Authentication.SignOut(DefaultAuthenticationTypes.ExternalCookie);

            AuthenticationTicket ticket = AccessTokenFormat.Unprotect(model.ExternalAccessToken);

            if (ticket == null || ticket.Identity == null || (ticket.Properties != null
                && ticket.Properties.ExpiresUtc.HasValue
                && ticket.Properties.ExpiresUtc.Value < DateTimeOffset.UtcNow))
            {
                return BadRequest("External login failure.");
            }

            ExternalLoginData externalData = ExternalLoginData.FromIdentity(ticket.Identity);

            if (externalData == null)
            {
                return BadRequest("The external login is already associated with an account.");
            }

            IdentityResult result = await UserManager.AddLoginAsync(User.Identity.GetUserId(),
                new UserLoginInfo(externalData.LoginProvider, externalData.ProviderKey));

            if (!result.Succeeded)
            {
                return GetErrorResult(result);
            }

            return Ok();
        }

        // POST api/Account/RemoveLogin
        [Route("RemoveLogin")]
        public async Task<IHttpActionResult> RemoveLogin(RemoveLoginBindingModel model)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            IdentityResult result;

            if (model.LoginProvider == LocalLoginProvider)
            {
                result = await UserManager.RemovePasswordAsync(User.Identity.GetUserId());
            }
            else
            {
                result = await UserManager.RemoveLoginAsync(User.Identity.GetUserId(),
                    new UserLoginInfo(model.LoginProvider, model.ProviderKey));
            }

            if (!result.Succeeded)
            {
                return GetErrorResult(result);
            }

            return Ok();
        }

        // GET api/Account/ExternalLogin
        [OverrideAuthentication]
        [HostAuthentication(DefaultAuthenticationTypes.ExternalCookie)]
        [AllowAnonymous]
        [Route("ExternalLogin", Name = "ExternalLogin")]
        public async Task<IHttpActionResult> GetExternalLogin(string provider, string error = null)
        {
            if (error != null)
            {
                return Redirect(Url.Content("~/") + "#error=" + Uri.EscapeDataString(error));
            }

            if (!User.Identity.IsAuthenticated)
            {
                return new ChallengeResult(provider, this);
            }

            ExternalLoginData externalLogin = ExternalLoginData.FromIdentity(User.Identity as ClaimsIdentity);

            if (externalLogin == null)
            {
                return InternalServerError();
            }

            if (externalLogin.LoginProvider != provider)
            {
                Authentication.SignOut(DefaultAuthenticationTypes.ExternalCookie);
                return new ChallengeResult(provider, this);
            }

            ApplicationUser user = await UserManager.FindAsync(new UserLoginInfo(externalLogin.LoginProvider,
                externalLogin.ProviderKey));

            bool hasRegistered = user != null;

            if (hasRegistered)
            {
                Authentication.SignOut(DefaultAuthenticationTypes.ExternalCookie);
                
                 ClaimsIdentity oAuthIdentity = await user.GenerateUserIdentityAsync(UserManager,
                    OAuthDefaults.AuthenticationType);
                ClaimsIdentity cookieIdentity = await user.GenerateUserIdentityAsync(UserManager,
                    CookieAuthenticationDefaults.AuthenticationType);

                AuthenticationProperties properties = ApplicationOAuthProvider.CreateProperties(user.UserName);
                Authentication.SignIn(properties, oAuthIdentity, cookieIdentity);
            }
            else
            {
                IEnumerable<Claim> claims = externalLogin.GetClaims();
                ClaimsIdentity identity = new ClaimsIdentity(claims, OAuthDefaults.AuthenticationType);
                Authentication.SignIn(identity);
            }

            return Ok();
        }

        // GET api/Account/ExternalLogins?returnUrl=%2F&generateState=true
        [AllowAnonymous]
        [Route("ExternalLogins")]
        public IEnumerable<ExternalLoginViewModel> GetExternalLogins(string returnUrl, bool generateState = false)
        {
            IEnumerable<AuthenticationDescription> descriptions = Authentication.GetExternalAuthenticationTypes();
            List<ExternalLoginViewModel> logins = new List<ExternalLoginViewModel>();

            string state;

            if (generateState)
            {
                const int strengthInBits = 256;
                state = RandomOAuthStateGenerator.Generate(strengthInBits);
            }
            else
            {
                state = null;
            }

            foreach (AuthenticationDescription description in descriptions)
            {
                ExternalLoginViewModel login = new ExternalLoginViewModel
                {
                    Name = description.Caption,
                    Url = Url.Route("ExternalLogin", new
                    {
                        provider = description.AuthenticationType,
                        response_type = "token",
                        client_id = Startup.PublicClientId,
                        redirect_uri = new Uri(Request.RequestUri, returnUrl).AbsoluteUri,
                        state = state
                    }),
                    State = state
                };
                logins.Add(login);
            }

            return logins;
        }

        // POST api/Account/Register
        [AllowAnonymous]
        [Route("Register")]
        public async Task<IHttpActionResult> Register(RegisterBindingModel model)
        {
            try
            {
                //Utility.eBayItem.GetStore(11, "03d519ce-2f86-4bb7-8f26-e19fade3e261");
                //Utility.eBayItem.GetStore(1, "65e09eec-a014-4526-a569-9f2d3600aa89");
                //Utility.eBayItem.GetStore(3, "9b63b57d-8839-4ed2-ba98-fad513c4ecec");

                //await DeleteUsrAsync("baf38b89-7007-429e-984e-ff00afb4dd91");
                //await DeleteUsrAsync("78cd1257-a514-4f30-87e5-316369ef0488");
                //await DeleteUsrAsync("b21112f2-bc3e-4540-8587-229b2b1ed0b3");

                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }
                var user = new ApplicationUser() { UserName = model.Username, Email = model.Email };
                IdentityResult result = await UserManager.CreateAsync(user, model.Password);
                if (!result.Succeeded)
                {
                    return GetErrorResult(result);
                }
                var p = new UserProfile();
                p.UserID = user.Id;
                p.Firstname = model.Firstname;
                p.Lastname = model.Lastname;
                p.Created = DateTime.Now;

                var settings = new UserSettingsView { UserName = model.Username };
                await _repository.UserProfileSaveAsync(p);

                return Ok();
            }
            catch (Exception exc)
            {
                
                string msg = dsutil.DSUtil.ErrMsg("Register", exc);
                dsutil.DSUtil.WriteFile(_logfile, msg, "admin");
                return Content(HttpStatusCode.InternalServerError, msg);
            }
        }

        private IHttpActionResult GetErrorResultDetail(IdentityResult result)
        {
            if (result == null)
            {
                return InternalServerError();
            }
            if (!result.Succeeded)
            {
                if (result.Errors != null)
                {
                    foreach (string error in result.Errors)
                    {
                        ModelState.AddModelError("", error);
                    }
                }
                if (ModelState.IsValid)
                {
                    // No ModelState errors are available to send, so just return an empty BadRequest.
                    return BadRequest();
                }
                return BadRequest(ModelState);
            }
            return null;
        }
        // POST api/Account/RegisterExternal
        [OverrideAuthentication]
        [HostAuthentication(DefaultAuthenticationTypes.ExternalBearer)]
        [Route("RegisterExternal")]
        public async Task<IHttpActionResult> RegisterExternal(RegisterExternalBindingModel model)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }
            var info = await Authentication.GetExternalLoginInfoAsync();
            if (info == null)
            {
                return InternalServerError();
            }

            var user = new ApplicationUser() { UserName = model.Email, Email = model.Email };
            IdentityResult result = await UserManager.CreateAsync(user);
            if (!result.Succeeded)
            {
                return GetErrorResult(result);
            }
            result = await UserManager.AddLoginAsync(user.Id, info.Login);
            if (!result.Succeeded)
            {
                return GetErrorResult(result); 
            }
            return Ok();
        }
        [HttpPost, ActionName("Delete")]
        public async Task<IHttpActionResult> DeleteUser(string id)
        {
            try
            {
                await DeleteUsrAsync(id);
                return Ok();
            }
            catch (Exception exc)
            {
                string msg = dsutil.DSUtil.ErrMsg("DeleteUser", exc);
                return Content(HttpStatusCode.InternalServerError, msg);
            }
        }
        protected async Task DeleteUsrAsync(string id)
        {
            var profile = _repository.Context.UserProfiles.SingleOrDefault(p => p.UserID == id);
            if (profile != null)
            {
                string ret = await _repository.UserProfileDeleteAsync(profile);
                var user = await UserManager.FindByIdAsync(id);

                //ApplicationUserManager manager = HttpContext.Current.GetOwinContext().GetUserManager<ApplicationUserManager>();
                //ApplicationUser appuser = await manager.FindByEmailAsync(user.Email);
                await UserManager.DeleteAsync(user);
            }           
            else
            {
                var user = await UserManager.FindByIdAsync(id);
                await UserManager.DeleteAsync(user);
            }
        }

        /// <summary>
        /// Decided to keep this as found at stackoverflow link.
        /// https://stackoverflow.com/questions/23977036/asp-net-mvc-5-how-to-delete-a-user-and-its-related-data-in-identity-2-0 
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        // 
        // POST: /Users/Delete/5
        [HttpPost, ActionName("Delete")]
        public async Task<IHttpActionResult> DeleteConfirmed(string id)
        {
            if (ModelState.IsValid)
            {
                if (id == null)
                {
                    return BadRequest("invalid id");
                }
                var user = await _userManager.FindByIdAsync(id);
                var logins = user.Logins;
                var rolesForUser = await _userManager.GetRolesAsync(id);

                using (var transaction = _repository.Context.Database.BeginTransaction())
                {
                    foreach (var login in logins.ToList())
                    {
                        await _userManager.RemoveLoginAsync(login.UserId, new UserLoginInfo(login.LoginProvider, login.ProviderKey));
                    }

                    if (rolesForUser.Count() > 0)
                    {
                        foreach (var item in rolesForUser.ToList())
                        {
                            // item should be the name of the role
                            var result = await _userManager.RemoveFromRoleAsync(user.Id, item);
                        }
                    }
                    await _userManager.DeleteAsync(user);
                    transaction.Commit();
                }

                return Ok();
            }
            else
            {
                return BadRequest("modelstate is invalid");
            }
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing && _userManager != null)
            {
                _userManager.Dispose();
                _userManager = null;
            }

            base.Dispose(disposing);
        }

        /*
        [HttpDelete]
        [Route("deleteapikey/{appID}")]
        [AcceptVerbs("DELETE")]
        public async Task<IHttpActionResult> DeleteAPIKey(string appID)
        {
            try
            {
                await models.AppIDRemove(appID);
                return Ok();
            }
            catch (Exception exc)
            {
                string msg = exc.Message;
                return Content(HttpStatusCode.InternalServerError, msg);
            }
        }
        */

        #region Helpers

        private IAuthenticationManager Authentication
        {
            get { return Request.GetOwinContext().Authentication; }
        }

        private IHttpActionResult GetErrorResult(IdentityResult result)
        {
            if (result == null)
            {
                return InternalServerError();
            }

            if (!result.Succeeded)
            {
                if (result.Errors != null)
                {
                    foreach (string error in result.Errors)
                    {
                        ModelState.AddModelError("", error);
                    }
                }

                if (ModelState.IsValid)
                {
                    // No ModelState errors are available to send, so just return an empty BadRequest.
                    return BadRequest();
                }

                return BadRequest(ModelState);
            }

            return null;
        }

        private class ExternalLoginData
        {
            public string LoginProvider { get; set; }
            public string ProviderKey { get; set; }
            public string UserName { get; set; }

            public IList<Claim> GetClaims()
            {
                IList<Claim> claims = new List<Claim>();
                claims.Add(new Claim(ClaimTypes.NameIdentifier, ProviderKey, null, LoginProvider));

                if (UserName != null)
                {
                    claims.Add(new Claim(ClaimTypes.Name, UserName, null, LoginProvider));
                }

                return claims;
            }

            public static ExternalLoginData FromIdentity(ClaimsIdentity identity)
            {
                if (identity == null)
                {
                    return null;
                }

                Claim providerKeyClaim = identity.FindFirst(ClaimTypes.NameIdentifier);

                if (providerKeyClaim == null || String.IsNullOrEmpty(providerKeyClaim.Issuer)
                    || String.IsNullOrEmpty(providerKeyClaim.Value))
                {
                    return null;
                }

                if (providerKeyClaim.Issuer == ClaimsIdentity.DefaultIssuer)
                {
                    return null;
                }

                return new ExternalLoginData
                {
                    LoginProvider = providerKeyClaim.Issuer,
                    ProviderKey = providerKeyClaim.Value,
                    UserName = identity.FindFirstValue(ClaimTypes.Name)
                };
            }
        }

        private static class RandomOAuthStateGenerator
        {
            private static RandomNumberGenerator _random = new RNGCryptoServiceProvider();

            public static string Generate(int strengthInBits)
            {
                const int bitsPerByte = 8;

                if (strengthInBits % bitsPerByte != 0)
                {
                    throw new ArgumentException("strengthInBits must be evenly divisible by 8.", "strengthInBits");
                }

                int strengthInBytes = strengthInBits / bitsPerByte;

                byte[] data = new byte[strengthInBytes];
                _random.GetBytes(data);
                return HttpServerUtility.UrlTokenEncode(data);
            }
        }

        #endregion

        [HttpPost]
        [Route("userprofilesave")]
        public async Task<IHttpActionResult> UserProfileSave(UserProfile profile)
        {
            IUserSettingsView settings = new UserSettingsView();
            try
            {
                string connStr = ConfigurationManager.ConnectionStrings["OPWContext"].ConnectionString;
                settings = _repository.GetUserSettingsView(connStr, profile.UserID);

                await _repository.UserProfileSaveAsync(profile, "SelectedStore");
                return Ok();
            }
            catch (Exception exc)
            {
                string msg = dsutil.DSUtil.ErrMsg("UserProfileSave", exc);
                dsutil.DSUtil.WriteFile(_logfile, msg, settings.UserName);
                return BadRequest(msg);
            }
        }
    }
}
