using System.Collections;
using Morph.Endpoint;
using Morph.Params;
using MorphDemoBooking;

namespace MorphDemoBookingServer
{
    /* Multiple clients can claim to have the same name.  This class ensures
     * that each client is distinguished by its apartment ID instead of its name.
     */
    public class Registration
    {
        #region Static

        static Registration()
        {
            ObjectInstance.OnClientIDChanged += OnClientIDChanged;
        }

        private static readonly Hashtable s_allRegistrations = new Hashtable();

        static public Registration FindBy(string clientID)
        {
            if (clientID == null)
                return null;
            else
                lock (s_allRegistrations)
                    return ((Registration)s_allRegistrations[clientID]);
        }

        static public string ClientID_To_ClientName(string clientID)
        {
            if (clientID == null)
                return null;
            Registration reg = FindBy(clientID);
            if (reg != null)
                return reg._clientName;
            else
                return null;
        }

        #endregion

        public Registration(string clientName, BookingDiplomatClientProxy clientProxy, MorphApartment apartment)
        {
            //  Client identification
            _clientName = clientName;
            _clientID = apartment.ID.ToString();
            //  Keep track of remote diplomat
            _clientProxy = clientProxy;
            //  Keep track of local diplomat
            _serverImpl = new BookingDiplomatServerImpl();
            _serverImpl._Registration = this;
            //  Register the Server servlet with this session's apartment
            _serverImpl.MorphApartment = apartment;
            //  Register the Registration
            lock (s_allRegistrations)
                s_allRegistrations[_clientID] = this;
        }

        public void Unregister()
        {
            //  Let other clients have access to objects
            lock (s_allRegistrations)
                ObjectInstances.ReleaseAll(_clientID);
            //  Unregister the Registration
            s_allRegistrations[_clientID] = null;
            _serverImpl.MorphApartment.Dispose();
        }

        public string _clientName;
        public string _clientID;
        public BookingDiplomatClientProxy _clientProxy;
        public BookingDiplomatServerImpl _serverImpl;

        // This event method notifies all interested clients when object ownership has changed
        static void OnClientIDChanged(object sender, ClientIDArgs args)
        {
            ObjectInstance obj = args.ObjectInstance;
            //  Get new client name
            string newClientName = Registration.ClientID_To_ClientName(args.NewClientID);
            //  List all clients who are waiting for this object
            string[] clientIDs = ObjectInstances.ListClientIDs(obj.ObjectName);
            //  Tell each client that is interested in this object that the owner has changed
            for (int i = 0; i < clientIDs.Length; i++)
            {
                Registration waitingClient = FindBy(clientIDs[i]);
                try
                { //  Tell the waiting client about the change of owner
                    waitingClient._clientProxy.NewOwner(obj.ObjectName, newClientName);
                }
                catch
                { //  Zero tolerance.  If there's a problem, then unregister the client
                    waitingClient.Unregister();
                }
            }
        }
    }

    public class BookingRegistrationImpl : MorphReference, IBookingRegistration
    {
        public BookingRegistrationImpl()
          : base(BookingInterface.RegistrationTypeName)
        {
        }

        #region BookingRegistration Members

        public IBookingDiplomatServer Register(string clientName, IBookingDiplomatClient client)
        {
            BookingDiplomatClientProxy clientProxy = (BookingDiplomatClientProxy)client;
            Registration registration = new Registration(clientName, clientProxy, this.MorphApartment);
            return registration._serverImpl;
        }

        #endregion
    }

    public class BookingDiplomatServerImpl : MorphReference, IBookingDiplomatServer
    {
        public BookingDiplomatServerImpl()
          : base(BookingInterface.DiplomatServerTypeName)
        {
        }

        public Registration _Registration;

        #region BookingDiplomatServer Members

        public string Book(string objectName)
        {
            string newOwnersClientID = ObjectInstances.Obtain(objectName, _Registration._clientID);
            return Registration.ClientID_To_ClientName(newOwnersClientID);
        }

        public string Unbook(string objectName)
        {
            string newOwnersClientID = ObjectInstances.Release(objectName, _Registration._clientID);
            return Registration.ClientID_To_ClientName(newOwnersClientID);
        }

        public string OwnerOf(string objectName)
        {
            string ownersClientID = ObjectInstances.CurrentOwnerOf(objectName);
            return Registration.ClientID_To_ClientName(ownersClientID);
        }

        public string[] GetQueue(string objectName)
        {
            string[] clientIDs = ObjectInstances.ListClientIDs(objectName);
            string[] clientNames = new string[clientIDs.Length];
            for (int i = 0; i < clientIDs.Length; i++)
                clientNames[i] = Registration.ClientID_To_ClientName(clientIDs[i]);
            return clientNames;
        }

        public void Nudge(string objectName)
        {
            string ownerID = ObjectInstances.CurrentOwnerOf(objectName);
            if (ownerID != null)
            {
                Registration currentOwner = Registration.FindBy(ownerID);
                try
                { //  Nudge the current owner of the object, telling them who the nudge is from
                    currentOwner._clientProxy.NudgedBy(_Registration._clientName);
                }
                catch
                { //  Zero tolerance.  If there's a problem, then unregister the client
                    currentOwner.Unregister();
                }
            }
        }

        #endregion
    }

    public class BookingDiplomatClientProxy : IBookingDiplomatClient
    {
        public BookingDiplomatClientProxy(ServletProxy ServletProxy)
        {
            _servletProxy = ServletProxy;
        }

        private ServletProxy _servletProxy;

        #region BookingDiplomatClient Members

        public void NewOwner(string objectName, string clientName)
        {
            _servletProxy.SendMethod("NewOwner", new object[] { objectName, clientName });
        }

        public void NudgedBy(string clientName)
        {
            _servletProxy.CallMethod("NudgedBy", new object[] { clientName }, false);
        }

        #endregion
    }
}