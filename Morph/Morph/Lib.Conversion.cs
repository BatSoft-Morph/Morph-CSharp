using System;
using System.Text;

namespace Morph.Lib
{
    public static class Conversion
    {
        #region Date/Time

        //  DateTime encoded according to http://www.w3.org/TR/xmlschema-2/#dateTime

        static private void AppendFullNumber(StringBuilder builder, int number, int length)
        {
            string str = number.ToString();
            for (int i = length - str.Length; i > 0; i--)
                builder.Append('0');
            builder.Append(number);
        }

        static public string DateTimeToStr(DateTime when)
        {
            StringBuilder builder = new StringBuilder(21);
            builder.Append(when.Year);
            builder.Append('-');
            AppendFullNumber(builder, when.Month, 2);
            builder.Append('-');
            AppendFullNumber(builder, when.Day, 2);
            builder.Append('T');
            AppendFullNumber(builder, when.Hour, 2);
            builder.Append(':');
            AppendFullNumber(builder, when.Minute, 2);
            builder.Append(':');
            AppendFullNumber(builder, when.Second, 2);
            //  "Instant is always represented by a date/time in UTC." (with 'Z')
            //  "Local time is always represented by a date/time without 'Z'."
            if (when.Kind == DateTimeKind.Utc)
                builder.Append('Z');
            return builder.ToString();
        }

        static public DateTime StrToDateTime(string when)
        {
            StringParser parser = new StringParser(when);
            //  Year (could be negative)
            int year;
            string yearStr = parser.ReadTo("-", true);
            if (yearStr == null)
                year = -Int32.Parse(parser.ReadTo("-", true));
            else
                year = Int32.Parse(yearStr);
            //  Read the rest of the date/time
            int month = Int32.Parse(parser.ReadTo("-", true));
            int day = Int32.Parse(parser.ReadTo("T", true));
            int hour = Int32.Parse(parser.ReadTo(":", true));
            int minute = Int32.Parse(parser.ReadTo(":", true));
            int second = Int32.Parse(parser.ReadDigits());
            //  Milliseconds
            int ms = 0;
            if (!parser.IsEnded())
                if (parser.Current() == '.')
                {
                    parser.Move(1);
                    ms = Int32.Parse(parser.ReadDigits());
                }
            //  Time zone
            DateTimeKind kind;
            if (parser.IsEnded())
                kind = DateTimeKind.Local;
            else if (parser.Current() == 'Z')
                kind = DateTimeKind.Utc;
            else
            {
                DateTime result = new DateTime(year, month, day, hour, minute, second, ms, new System.Globalization.GregorianCalendar(), DateTimeKind.Utc);
                int tzHour = Int32.Parse(parser.ReadTo(":", true));
                int tzMinute = Int32.Parse(parser.ReadToEnd());
                result = result.AddHours(-tzHour);
                result = result.AddMinutes(-tzMinute);
                return result;
            }
            return new DateTime(year, month, day, hour, minute, second, ms, new System.Globalization.GregorianCalendar(), kind);
        }

        #endregion
    }
}