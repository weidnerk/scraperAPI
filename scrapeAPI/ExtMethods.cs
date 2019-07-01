using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.ComponentModel;
using System.Xml.Linq;

namespace scrapeAPI
{
    public static class ExtMethods
    {

        public static string TryGetElementValue(this XElement parentEl, string elementName, string defaultValue = null)
        {
            try
            {
                var foundEl = parentEl.Element(elementName);

                if (foundEl != null)
                {
                    return foundEl.Value;
                }

                return defaultValue;
            }
            catch (Exception exc)
            {
                string s = exc.Message;
                return null;
            }
        }

        public static string ElementValueNull(this XElement element)
        {
            if (element != null)
                return element.Value;

            return "";
        }

        public static string ElementNameValueNull(this XElement element, string elementName)
        {
            if (element == null)
                return "";
            else
            {
                XElement ele = element.Element(elementName);
                return ele == null ? "" : ele.Value;
            }
        }

        //This method is to handle if attribute is missing
        public static string AttributeValueNull(this XElement element, string attributeName)
        {
            if (element == null)
                return "";
            else
            {
                XAttribute attr = element.Attribute(attributeName);
                return attr == null ? "" : attr.Value;
            }
        }

        public static object OrDbNull(this string s)
        {
            return string.IsNullOrEmpty(s) ? DBNull.Value : (object)s;
        }
        public static object OrDbNull(this DateTime? d)
        {
            return !d.HasValue ? DBNull.Value : (object)d;
        }

        /// <summary>
        /// https://stackoverflow.com/questions/2961656/generic-tryparse 
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="input"></param>
        /// <returns></returns>
        public static T ConvertToNumber<T>(this string input)
        {
            try
            {
                var converter = TypeDescriptor.GetConverter(typeof(T));
                if (converter != null)
                {
                    // Cast ConvertFromString(string text) : object to (T)
                    return (T)converter.ConvertFromString(input);
                }
                return default(T);
            }
            catch (NotSupportedException)
            {
                return default(T);
            }
        }
    }
}