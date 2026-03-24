using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Bat.Library.Logging
{
    public class Log
    {
        public Log(string FileName)
        {
            _fileName = FileName;
        }

        public const string nl = "\u000D\u000A";

        public static Log Default = new Log(LogFileName());
        private static readonly Encoding s_encoding = Encoding.UTF8;

        private readonly string _fileName;
        public string FileName
        {
            get { return _fileName; }
        }

        public void Add(string message)
        {
            FileStream stream;
            if (File.Exists(_fileName))
                stream = new FileStream(_fileName, FileMode.Append);
            else
                stream = new FileStream(_fileName, FileMode.Create);
            try
            {
                byte[] bytes = s_encoding.GetBytes(message + nl);
                stream.Write(bytes, 0, bytes.Length);
            }
            finally
            {
                stream.Flush();
                stream.Close();
            }
        }

        public void Add(int value)
        {
            Add(value.ToString());
        }

        public void Add(Object obj)
        {
            Add(ObjectToString(obj));
        }

        public void Add(string message, Object obj)
        {
            Add(message + ' ' + ObjectToString(obj));
        }

        public String ObjectToString(Object obj)
        {
            if (obj == null)
                return "null";
            foreach (ILogType type in Types)
            {
                String str = type.ToString(obj);
                if (str != null)
                    return str;
            }
            return obj.ToString();
        }

        public List<ILogType> Types = new List<ILogType>();

        private static string LogFileName()
        {
            string fileName = System.Reflection.Assembly.GetExecutingAssembly().GetName().CodeBase + ".log";
            if ("file:///".Equals(fileName.Substring(0, 8)))
                fileName = fileName.Substring(8);
            return fileName;
        }
    }

    public interface ILogType
    {
        String ToString(Object obj);
    }
}