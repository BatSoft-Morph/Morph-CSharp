using Morph.Endpoint;

namespace Basic
{
    public class BasicDefaultProxy : BasicDefault
    {
        public BasicDefaultProxy(ServletProxy Proxy)
          : base()
        {
            _Proxy = Proxy;
        }

        private ServletProxy _Proxy;

        private BasicSimple _Simple = null;
        public BasicSimple simple
        {
            get
            {
                if (_Simple == null)
                {
                    object o = _Proxy.CallGetProperty("simple", null);
                    _Simple = (BasicSimple)o;
                }
                return _Simple;
            }
        }

        private BasicStructs _Structs = null;
        public BasicStructs structs
        {
            get
            {
                if (_Structs == null)
                    _Structs = (BasicStructs)_Proxy.CallGetProperty("structs", null);
                return _Structs;
            }
        }

        private BasicArrays _Arrays = null;
        public BasicArrays arrays
        {
            get
            {
                if (_Arrays == null)
                    _Arrays = (BasicArrays)_Proxy.CallGetProperty("arrays", null);
                return _Arrays;
            }
        }

        private BasicExceptions _Exceptions = null;
        public BasicExceptions exceptions
        {
            get
            {
                if (_Exceptions == null)
                    _Exceptions = (BasicExceptions)_Proxy.CallGetProperty("exceptions", null);
                return _Exceptions;
            }
        }
    }

    public class BasicSimpleProxy : BasicSimple
    {
        public BasicSimpleProxy(ServletProxy Proxy)
          : base()
        {
            _Proxy = Proxy;
        }

        private ServletProxy _Proxy;

        public void assignNumber(int number)
        {
            _Proxy.CallMethod("assignNumber", new object[] { number }, false);
        }

        public int retrieveNumber()
        {
            return (int)_Proxy.CallMethod("retrieveNumber", null, true);
        }

        public void assignText(string text)
        {
            _Proxy.CallMethod("assignText", new object[] { text }, false);
        }

        public string retrieveText()
        {
            return (string)_Proxy.CallMethod("retrieveText", null, true);
        }

        public int number
        {
            get { return (int)_Proxy.CallGetProperty("number", null); }
            set { _Proxy.CallSetProperty("number", value, null); }
        }

        public string text
        {
            get { return (string)_Proxy.CallGetProperty("text", null); }
            set { _Proxy.CallSetProperty("text", value, null); }
        }
    }

    public class BasicStructsProxy : BasicStructs
    {
        public BasicStructsProxy(ServletProxy Proxy)
          : base()
        {
            _Proxy = Proxy;
        }

        private ServletProxy _Proxy;

        public void assignStruct(BasicStruct aStruct)
        {
            _Proxy.CallMethod("assignStruct", new object[] { aStruct }, false);
        }

        public BasicStruct retrieveStruct()
        {
            return (BasicStruct)_Proxy.CallMethod("retrieveStruct", null, true);
        }

        public void assignObject(BasicClass aObject)
        {
            _Proxy.CallMethod("assignObject", new object[] { aObject }, false);
        }

        public BasicClass retrieveObject()
        {
            return (BasicClass)_Proxy.CallMethod("retrieveObject", null, true);
        }
    }

    public class BasicArraysProxy : BasicArrays
    {
        public BasicArraysProxy(ServletProxy Proxy)
          : base()
        {
            _Proxy = Proxy;
        }

        private ServletProxy _Proxy;

        public void assignChars(char[] chars)
        {
            _Proxy.CallMethod("assignChars", new object[] { chars }, false);
        }

        public char[] retrieveChars()
        {
            return (char[])_Proxy.CallMethod("retrieveChars", null, true);
        }
    }

    public class BasicExceptionsProxy : BasicExceptions
    {
        public BasicExceptionsProxy(ServletProxy Proxy)
          : base()
        {
            _Proxy = Proxy;
        }

        private ServletProxy _Proxy;

        public void custom()
        {
            _Proxy.CallMethod("custom", null, false);
        }

        public void morph()
        {
            _Proxy.CallMethod("morph", null, false);
        }
    }
}