using System.Collections;
using Morph.Lib;

namespace Morph.Endpoint
{
    public class Servlet
    {
        internal Servlet(MorphApartment apartment, int id, object obj, string typeName)
        {
            _apartment = apartment;
            _id = id;
            _object = obj;
            _typeName = typeName;
        }

        private readonly MorphApartment _apartment;
        public MorphApartment Apartment
        {
            get => _apartment;
        }

        public const int DefaultID = 0;

        private readonly int _id;
        public int ID
        {
            get => _id;
        }

        private readonly object _object;
        public object Object
        {
            get => _object;
        }

        private readonly string _typeName;
        public string TypeName
        {
            get => _typeName;
        }
    }

    public class Servlets
    {
        internal Servlets(MorphApartment apartment, object defaultServletObject)
        {
            _apartment = apartment;
            _default = Obtain(defaultServletObject);
        }

        private readonly MorphApartment _apartment;

        private readonly IDSeed _servletIDSeed = new IDSeed(Servlet.DefaultID);
        private readonly Hashtable _servlets = new Hashtable();

        private readonly Servlet _default;
        public Servlet Default
        {
            get => _default;
        }

        public Servlet Obtain(object servletObject)
        {
            return Obtain(servletObject, null);
        }

        public Servlet Obtain(object servletObject, string typeName)
        {
            Servlet servlet = new Servlet(_apartment, _servletIDSeed.Generate(), servletObject, typeName);
            lock (_servlets)
                _servlets.Add(servlet.ID, servlet);
            return servlet;
        }

        public void Remove(int servletID)
        {
            if (servletID == _default.ID)
                throw new EMorphUsage("Cannot deregister default servlet");
            lock (_servlets)
                _servlets.Remove(servletID);
        }

        public Servlet Find(int servletID)
        {
            if (servletID == _default.ID)
                return Default;
            lock (_servlets)
                return (Servlet)(_servlets[servletID]);
        }
    }
}