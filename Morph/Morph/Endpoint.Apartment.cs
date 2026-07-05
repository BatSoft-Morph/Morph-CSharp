#region Description
/* Essentially, an apartment represents a logical memory space on a device.
 * Servlets represent objects within that memory space.
 * 
 * For any remote procedure call, an apartment represents the called endpoint.
 * So, a method call will be sent from an apartment proxy to an apartment, and
 * the method reply will be sent from the apartment to the apartment proxy.
 */
#endregion

#region Client side usage
#endregion

using System;
using System.Collections.Generic;
using Morph.Base;
using Morph.Core;
using Morph.Lib;
using Morph.Params;
using Morph.Sequencing;

namespace Morph.Endpoint
{
    public abstract class MorphApartment : IRegisterItemID, IDisposable, IActionLinkSequence, IActionLast
    {
        protected MorphApartment(MorphApartmentFactory owner, InstanceFactories instanceFactories, object defaultObject)
        {
            if (instanceFactories == null)
                throw new EMorphUsage("Apartment must have InstanceFactories object");
            _instanceFactories = instanceFactories;
            _owner = owner;
            _id = IDFactory.Generate();
            _servlets = new Servlets(this, defaultObject);
            MorphApartmentFactory.RegisterApartment(this);
        }

        #region IDisposable Members

        public virtual void Dispose()
        {
            try
            {
                IDFactory.Release(ID);
            }
            finally
            {
                MorphApartmentFactory.UnregisterApartment(this);
            }
        }

        #endregion

        #region RegisterItemID Members

        private readonly int _id;
        public int ID
        {
            get => _id;
        }

        #endregion

        public const int DefaultID = 0;

        static public IIDFactory IDFactory = new IDSeed(DefaultID + 1);

        private readonly MorphApartmentFactory _owner;
        public MorphApartmentFactory Owner
        {
            get => _owner;
        }

        public virtual void ResetTimeout()
        {
            //  May be used by subclass
        }

        public virtual Servlet DefaultServlet
        {
            get => Servlets.Find(Servlet.DefaultID);
        }

        private readonly Servlets _servlets;
        public Servlets Servlets
        {
            get => _servlets;
        }

        private readonly InstanceFactories _instanceFactories;
        public InstanceFactories InstanceFactories
        {
            get => _instanceFactories;
        }

        #region IActionLinkSequence

        public virtual void ActionLinkSequence(LinkMessage message, LinkSequence linkSequence)
        {
            //  Shared apartments don't use sequences
            throw new EMorph("Unexpected link type");
        }

        #endregion

        #region IActionLast

        public void ActionLast(LinkMessage message)
        {
            LinkMessage reply = message.CreateReply();
            if (reply != null)
                reply.NextLinkAction();
        }

        #endregion
    }

    public interface IDefaultServletObjectFactory
    {
        object ObtainServlet();
    }

    public abstract class MorphApartmentFactory
    {
        protected MorphApartmentFactory(InstanceFactories instanceFactories)
        {
            _instanceFactories = instanceFactories;
        }

        internal MorphService _service;

        private readonly InstanceFactories _instanceFactories;
        public InstanceFactories InstanceFactories
        {
            get => _instanceFactories;
        }

        static private RegisterItems<MorphApartment> s_all = new RegisterItems<MorphApartment>();

        static internal void RegisterApartment(MorphApartment apartment)
        {
            lock (s_all)
                MorphApartmentFactory.s_all.Add(apartment);
        }

        static internal void UnregisterApartment(MorphApartment apartment)
        {
            lock (s_all)
                MorphApartmentFactory.s_all.Remove(apartment);
        }

        static public MorphApartment Find(int apartmentID)
        {
            lock (s_all)
                return s_all.Find(apartmentID);
        }

        static public MorphApartment Obtain(int apartmentID)
        {
            if (apartmentID == MorphApartment.DefaultID)
                throw new EMorph("Obtain a default apartment by specifing a service.");
            //  Lookup existing apartment
            MorphApartment apartment = Find(apartmentID);
            if (apartment == null)
                throw new EMorph("Apartment not found");
            //  Keep session apartments alive
            apartment.ResetTimeout();
            return apartment;
        }

        public abstract MorphApartment ObtainDefault();

        protected internal virtual void ShutDown()
        {
            lock (s_all)
            {
                List<MorphApartment> allApartments = s_all.List();
                foreach (MorphApartment apartment in allApartments)
                    if (apartment.Owner == this)
                        apartment.Dispose();
            }
        }
    }
}