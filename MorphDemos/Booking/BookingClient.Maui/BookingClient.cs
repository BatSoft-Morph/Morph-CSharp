using Morph.Endpoint;
using Morph.Params;
using MorphDemoBooking;

namespace BookingClient.Maui
{
    //  The page implements this so the client's callback servlet can reach the UI.
    //  Morph invokes NewOwner/NudgedBy on a background thread;  the page marshals to the UI thread.
    public interface IBookingClientUI
    {
        void NewOwner(string objectName, string clientName);
        void NudgedBy(string clientName);
    }

    public class BookingRegistrationProxy : IBookingRegistration
    {
        public BookingRegistrationProxy(ServletProxy servletProxy)
        {
            _servletProxy = servletProxy;
        }

        private readonly ServletProxy _servletProxy;

        #region BookingRegistration Members

        public IBookingDiplomatServer Register(string clientName, IBookingDiplomatClient client)
        {
            return (IBookingDiplomatServer)_servletProxy.CallMethod("Register", new object[] { clientName, client }, true);
        }

        #endregion
    }

    public class BookingDiplomatServerProxy : IBookingDiplomatServer
    {
        public BookingDiplomatServerProxy(ServletProxy servletProxy)
        {
            _servletProxy = servletProxy;
        }

        private readonly ServletProxy _servletProxy;
        public ServletProxy ServletProxy
        {
            get { return _servletProxy; }
        }

        #region BookingDiplomatServer Members

        public string Book(string objectName)
        {
            return (string)_servletProxy.CallMethod("Book", new object[] { objectName }, true);
        }

        public string Unbook(string objectName)
        {
            return (string)_servletProxy.CallMethod("Unbook", new object[] { objectName }, true);
        }

        public string OwnerOf(string objectName)
        {
            return (string)_servletProxy.CallMethod("OwnerOf", new object[] { objectName }, true);
        }

        public string[] GetQueue(string objectName)
        {
            return (string[])_servletProxy.CallMethod("GetQueue", new object[] { objectName }, true);
        }

        public void Nudge(string objectName)
        {
            _servletProxy.SendMethod("Nudge", new object[] { objectName });
        }

        #endregion
    }

    public class BookingDiplomatClientImpl : MorphReference, IBookingDiplomatClient
    {
        public BookingDiplomatClientImpl(MorphApartment apartment, IBookingClientUI ui)
          : base(BookingInterface.DiplomatClientTypeName)
        {
            _ui = ui;
            MorphApartment = apartment;
        }

        private readonly IBookingClientUI _ui;

        #region BookingDiplomatClient Members

        public void NewOwner(string objectName, string clientName)
        {
            _ui.NewOwner(objectName, clientName);
        }

        public void NudgedBy(string clientName)
        {
            _ui.NudgedBy(clientName);
        }

        #endregion
    }

    public class BookingFactory : InstanceFactories
    {
        public BookingFactory()
          : base()
        {
            Add(new BookingDiplomatServerFactory());
        }

        private class BookingDiplomatServerFactory : IReferenceDecoder
        {
            #region IReferenceFactory Members

            public bool DecodeReference(ServletProxy value, out object reference)
            {
                if (BookingInterface.DiplomatServerTypeName.Equals(value.TypeName))
                {
                    reference = new BookingDiplomatServerProxy(value);
                    return true;
                }
                else
                {
                    reference = null;
                    return false;
                }
            }

            #endregion
        }
    }
}
