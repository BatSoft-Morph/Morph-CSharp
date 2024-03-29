﻿#region Server side usage
/* There are two ways for a service to provide an apartment.  A service may provide:
 * - a single *shared apartment* that replies to all calls
 * - a dedicated *session apartment* that communicates with only one "client"
 * 
 * Lifetime:
 * ---------
 * Session apartments are given a lifetime.  If no communication occurs during the timeout
 * then the apartment (ie. session) is lost.  Any communication to that apartment will 
 * reset the timer.
 * 
 * Default Servlet Object:
 * -----------------------
 * An service must have a default object.
 * The SOAP equivalent is its published interface.
 * 
 * For a service that provides a shared apartment, a single object will suffice.
 * For a service that provides session apartments, one might want to provide a unique
 * object for each session.  For this, implement the DefaultServletObjectFactory interface.
 * 
 * MorphApartment factories:
 * --------------------
 * A service will get an apartment from an factory.  Supply an MorphApartmentFactory factory to the
 * constructor of a service.
 */
#endregion

#region Examples
/* In these examples we publish a service named "Test".
 * The object to be published is of type TestClass.
 * The DefaultServletObjectFactory implementation that creates TestClass objects is TestFactory.
 * 
 * 1. Creating a shared service.  This is overkill, unless you want to do something fancy.
 * MorphServices.Register("Test", new MorphApartmentFactoryShared(new TestFactory()));
 * 
 * 2. We could use the factory that is already provided, which simply returns the given TestClass object.
 * MorphServices.Register("Test", new MorphApartmentFactoryShared(new DefaultServletObjectFactoryShared(new TestClass())));
 * 
 * 3. The easiest is to use the shortform:
 * MorphServices.Register("Test", new MorphApartmentFactoryShared(new TestClass()));
 * 
 * 4. Starting a sessioned sevice with a 5 minute idleness timeout:
 * MorphServices.Register("Test", new MorphApartmentFactorySession(new TestFactory(), new TimeSpan(0, 5, 0)));
 */
#endregion

using Morph.Lib;

namespace Morph.Endpoint
{
    public class MorphService : IRegisterItemID, IRegisterItemName
    {
        internal MorphService(string name, MorphApartmentFactory apartmentFactory)
        {
            if ((name == null) || (name.Length == 0))
                throw new EMorphUsage("A service needs a proper name.");
            if (apartmentFactory == null)
                throw new EMorphUsage("A service needs an Apartments strategy.");
            if (apartmentFactory._service != null)
                throw new EMorphUsage("Services cannot share the same ApartmentFactory object.");
            _name = name.ToLower();
            _apartmentFactory = apartmentFactory;
            _apartmentFactory._service = this;
        }

        #region MorphItemID Members

        public int ID
        {
            get => _name.GetHashCode();
        }

        #endregion

        #region MorphItemName Members

        private readonly string _name;
        public string Name
        {
            get => _name;
        }

        #endregion

        private readonly MorphApartmentFactory _apartmentFactory;
        public MorphApartmentFactory ApartmentFactory
        {
            get => _apartmentFactory;
        }

        public void Deregister()
        {
            MorphServices.Deregister(this);
        }
    }

    static public class MorphServices
    {
        private static readonly RegisterItems<MorphService> s_all = new RegisterItems<MorphService>();

        static public MorphService Register(string name, MorphApartmentFactory apartmentFactory)
        {
            MorphService service = new MorphService(name.ToLower(), apartmentFactory);
            lock (s_all)
                s_all.Add(service);
            return service;
        }

        static public void Deregister(string name)
        {
            lock (s_all)
                Deregister(s_all.Find(name.ToLower()));
        }

        static public void Deregister(MorphService service)
        {
            if (service == null)
                throw new EMorphUsage("Cannot stop a non-registered service");
            lock (s_all)
                s_all.Remove(service);
            service.ApartmentFactory.ShutDown();
        }

        static public MorphService Obtain(string name)
        {
            MorphService service;
            lock (s_all)
                service = s_all.Find(name.ToLower());
            if (service == null)
                throw new EMorph("Service not available: \"" + name + '\"');
            return service;
        }
    }
}