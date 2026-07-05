using Morph.Base;
using Morph.Endpoint;
using Morph.Internet;
using System;
using System.Collections;

namespace Morph.Daemon
{
    public abstract class RegisteredApartment : IDisposable
    {
        public RegisteredApartment(RegisteredApartments owner, int id)
        {
            _owner = owner;
            _id = id;
            _owner.Register(this);
        }

        #region IDisposable

        public virtual void Dispose()
        {
            _owner.Unregister(_id);
        }

        #endregion

        protected RegisteredApartments _owner;

        private readonly int _id;
        public int ID
        { get => _id; }

        public abstract bool CanUnregister
        { get; }

        public abstract void HandleMessage(LinkMessage message);
    }

    public class RegisteredApartmentDaemon : RegisteredApartment
    {
        public RegisteredApartmentDaemon(RegisteredApartments owner, int id)
          : base(owner, id)
        { }

        public override bool CanUnregister
        {
            get { return false; }
        }

        private readonly LinkTypeService _linkTypeService = new LinkTypeService();

        public override void HandleMessage(LinkMessage message)
        {
            _linkTypeService.ActionLink(message, message.Current);
        }
    }

    public class RegisteredApartmentInternet : RegisteredApartment
    {
        public RegisteredApartmentInternet(RegisteredApartments owner, int id, Connection connection)
          : base(owner, id)
        {
            lock (this)
            {
                _Connection = connection;
                _Connection.OnClose += ConnectionClose;
            }
        }

        public override void Dispose()
        {
            lock (this)
                if (_Connection != null)
                {
                    _Connection.OnClose -= ConnectionClose;
                    _Connection = null;
                }
            base.Dispose();
        }

        private Connection _Connection;

        private void ConnectionClose(object sender, EventArgs e)
        {
            Dispose();
        }

        public override bool CanUnregister
        {
            get => true;
        }

        public override void HandleMessage(LinkMessage message)
        {
            //  The relay connection may have been closed concurrently (see Dispose)
            Connection connection;
            lock (this)
                connection = _Connection;
            if (connection == null)
                throw new EMorphDaemon("Apartment connection has closed");
            connection.Write(message);
        }
    }

    public class RegisteredApartments
    {
        private readonly Hashtable _apartments = new Hashtable();

        public void Register(RegisteredApartment apartment)
        {
            lock (_apartments)
                if (_apartments[apartment.ID] == null)
                    _apartments.Add(apartment.ID, apartment);
                else
                    throw new EMorphDaemon("Duplicate apartment ID");
        }

        public void Unregister(int apartmentID)
        {
            lock (_apartments)
            {
                RegisteredApartment apartment = (RegisteredApartment)_apartments[apartmentID];
                if (apartment != null)
                    if (apartment.CanUnregister)
                        _apartments.Remove(apartmentID);
                    else
                        throw new EMorphDaemon("Cannot deregister this apartment");
            }
        }

        public RegisteredApartment Find(int apartmentID)
        {
            lock (_apartments)
            {
                RegisteredApartment apartment = (RegisteredApartment)_apartments[apartmentID];
                if (apartment == null)
                    throw new EMorphDaemon("Apartment ID not found: " + apartmentID.ToString());
                return apartment;
            }
        }

        static public RegisteredApartments Apartments = new RegisteredApartments();
        static public RegisteredApartments ApartmentProxies = new RegisteredApartments();
    }
}