using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace scrapeAPI
{
    public class Util
    {
        // convert array of strings to delimited string
        public static string ListToDelimited(string[] array, char limiter)
        {
            string s = "";
            foreach (string i in array)
                s += i + limiter;
            if (s.Length > 0)
                s = s.Substring(0, s.Length - 1);   // remove last semi colon
            return s;
        }

        // convert delimited string to list of strings
        public static List<string> DelimitedToList(string str, char limiter)
        {
            string[] array = str.Split(limiter);
            return array.ToList();
        }

        public static string GetErrMsg(Exception exc)
        {
            string msg = exc.Message;
            if (exc.InnerException != null)
            {
                msg += exc.InnerException.Message;
                if (exc.InnerException.InnerException != null)
                {
                    msg += exc.InnerException.InnerException.Message;
                }
            }
            return msg;
        }
    }
}