using Morph.Endpoint;
using Morph.Params;
using MorphDemoBooking;

namespace MorphDemoBookingClient
{
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
        { get { return _servletProxy; } }

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
        public BookingDiplomatClientImpl(MorphApartment apartment, BookingClientForm form)
          : base(BookingInterface.DiplomatClientTypeName)
        {
            _form = form;
            MorphApartment = apartment;
        }

        private readonly BookingClientForm _form;

        delegate void DelegateNewOwner(string objectName, string ClientName);
        delegate void DelegateNudgedBy(string ClientName);

        #region BookingDiplomatClient Members

        public void NewOwner(string objectName, string clientName)
        {
            _form.Invoke(new DelegateNewOwner(_form.NewOwner), new object[] { objectName, clientName });
        }

        public void NudgedBy(string clientName)
        {
            _form.Invoke(new DelegateNudgedBy(_form.NudgedBy), new object[] { clientName });
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