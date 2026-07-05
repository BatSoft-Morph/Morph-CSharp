using System;
using System.Xml;

namespace Morph.Lib
{
    public static class Conversion
    {
        #region Date/Time

        //  DateTime encoded according to http://www.w3.org/TR/xmlschema-2/#dateTime

        static public string DateTimeToStr(DateTime when)
        {
            //  Morph rule:  an instant (UTC) carries 'Z';  a local time carries no zone suffix;
            //  timezone offsets are never emitted.
            if (when.Kind == DateTimeKind.Utc)
                return XmlConvert.ToString(when, XmlDateTimeSerializationMode.Utc);
            else
                return XmlConvert.ToString(when, XmlDateTimeSerializationMode.Unspecified);
        }

        static public DateTime StrToDateTime(string when)
        {
            DateTime result = XmlConvert.ToDateTime(when, XmlDateTimeSerializationMode.RoundtripKind);
            //  Morph treats a value with no zone suffix as local time
            if (result.Kind == DateTimeKind.Unspecified)
                result = DateTime.SpecifyKind(result, DateTimeKind.Local);
            return result;
        }

        #endregion
    }
}
