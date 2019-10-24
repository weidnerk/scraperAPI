using dsmodels;
using eBay.Service.Core.Soap;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace scrapeAPI
{
    public class EbayCalls
    {
        static dsmodels.DataModelsDB db = new dsmodels.DataModelsDB();

        public static StoreProfile GetStoreProfile(int storeID)
        {
            var r = db.StoreProfiles.Where(p => p.ID == storeID).First();
            return r;
        }

        public static eBayAPIInterfaceService eBayServiceCall(UserSettingsView settings, string CallName)
        {
            string endpoint = AppSettingsHelper.Endpoint;
            string siteId = "0";
            string appId = settings.AppID;
            string devId = settings.DevID;
            string certId = settings.CertID;
            string version = "965";
            // Build the request URL
            string requestURL = endpoint
            + "?callname=" + CallName
            + "&siteid=" + siteId
            + "&appid=" + appId
            + "&version=" + version
            + "&routing=default";

            eBayAPIInterfaceService service = new eBayAPIInterfaceService();
            // Assign the request URL to the service locator.
            service.Url = requestURL;
            // Set credentials
            service.RequesterCredentials = new CustomSecurityHeaderType();
            service.RequesterCredentials.eBayAuthToken = settings.Token;
            service.RequesterCredentials.Credentials = new UserIdPasswordType();
            service.RequesterCredentials.Credentials.AppId = appId;
            service.RequesterCredentials.Credentials.DevId = devId;
            service.RequesterCredentials.Credentials.AuthCert = certId;
            return service;
        }
    }
}