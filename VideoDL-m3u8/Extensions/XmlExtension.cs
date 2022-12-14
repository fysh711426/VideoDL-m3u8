using System;
using System.Collections.Generic;
using System.Xml;
using System.Xml.Linq;

namespace VideoDL_m3u8.Extensions
{
    public static class XmlExtension
    {
        public static IEnumerable<XmlNode> AsEnumerable(this XmlNodeList xmlNodeList)
        {
            foreach (XmlNode item in xmlNodeList)
            {
                if (item != null)
                    yield return item;
            }
        }

        public static TimeSpan? ParseTimeSpan(this string val)
        {
            return val == null ? null : XmlConvert.ToTimeSpan(val);
        }

        public static bool? ParseBool(this string val)
        {
            return val == null ? null : bool.Parse(val);
        }
    }
}
