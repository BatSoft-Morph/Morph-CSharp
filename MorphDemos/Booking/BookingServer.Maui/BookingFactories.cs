using System;
using Morph.Daemon.Client;
using Morph.Endpoint;
using Morph.Params;
using Morph.Sequencing;
using MorphDemoBooking;

namespace MorphDemoBookingServer
{
    public class BookingRegistrationApartmentFactory : MorphApartmentFactorySession
    {
        public BookingRegistrationApartmentFactory(IDefaultServletObjectFactory defaultServletObject, InstanceFactories instanceFactories, TimeSpan timeout, SequenceLevel level)
          : base(defaultServletObject, instanceFactories, timeout, level)
        {
        }

        protected override MorphApartmentSession CreateApartment(object defaultObject, SequenceLevel level)
        {
            return new BookingRegistrationSession(this, defaultObject, level);
        }
    }

    public class BookingRegistrationSession : MorphApartmentSession
    {
        public BookingRegistrationSession(MorphApartmentFactory owner, object defaultObject, SequenceLevel level)
          : base(owner, defaultObject, level)
        {
            Count++;
        }

        public override void Dispose()
        {
            base.Dispose();
            //  If there are no more booking sessions, then shut down
            if ((--Count) == 0)
            {
                //  Both of these work when the application is visible.  (ie. manually started)
                //  But how to kill this application when it is not visible?  (ie. started by the Morph.Daemon)
                MorphManager.Shutdown();
                //BookingServerForm.Instance.Invoke(new VoidDelegate(BookingServerForm.Instance.Close));
                //BookingServerForm.Instance.Invoke(new VoidDelegate(Application.Exit));
            }
        }

        static private int Count = 0;

        private delegate void VoidDelegate();
    }

    public class BookingRegistrationFactory : IDefaultServletObjectFactory
    {
        #region DefaultServletObjectFactory Members

        public object ObtainServlet()
        {
            return new BookingRegistrationImpl();
        }

        #endregion
    }

    public class BookingInstanceFactories : InstanceFactories
    {
        public BookingInstanceFactories()
          : base()
        {
            Add(new BookingDiplomatClientFactory());
        }

        private class BookingDiplomatClientFactory : IReferenceDecoder
        {
            #region IReferenceFactory Members

            public bool DecodeReference(ServletProxy value, out object reference)
            {
                if (BookingInterface.DiplomatClientTypeName.Equals(value.TypeName))
                    reference = new BookingDiplomatClientProxy(value);
                else
                    reference = null;
                return reference != null;
            }

            #endregion
        }
    }
}