using Morph.Base;
using Morph.Core;

namespace Morph.Endpoint
{
    public class LinkService : Link
    {
        protected internal LinkService(string serviceName)
          : base(LinkTypeID.Service)
        {
            _serviceName = serviceName;
        }

        private readonly string _serviceName;
        public string ServiceName
        {
            get => _serviceName;
        }

        #region Link implementation

        public override int Size()
        {
            return 3 + MorphWriter.SizeOfString(_serviceName);
        }

        public override void Write(MorphWriter writer)
        {
            writer.WriteLinkByte(LinkTypeID, false, false, false);
            writer.WriteIdentifier(_serviceName);
        }

        #endregion

        public override bool Equals(object obj)
        {
            return (obj is LinkService linkService) && (linkService._serviceName.ToLower().Equals(_serviceName.ToLower()));
        }

        public override int GetHashCode()
        {
            return _serviceName.GetHashCode();
        }

        public override string ToString()
        {
            return "{Service Name=" + ServiceName + '}';
        }
    }

    public class LinkApartment : Link
    {
        public LinkApartment(int apartmentID)
          : base(LinkTypeID.Service)
        {
            _apartmentID = apartmentID;
        }

        public LinkApartment(MorphApartment apartment)
          : this(apartment.ID)
        {
        }

        private readonly int _apartmentID = MorphApartment.DefaultID;
        public int ApartmentID
        {
            get => _apartmentID;
        }

        #region Link implementation

        public override int Size()
        {
            return 5;
        }

        public override void Write(MorphWriter writer)
        {
            writer.WriteLinkByte(LinkTypeID, true, false, false);
            writer.WriteInt32(ApartmentID);
        }

        #endregion

        public override bool Equals(object obj)
        {
            return (obj is LinkApartment linkApartment) && (linkApartment._apartmentID == _apartmentID);
        }

        public override int GetHashCode()
        {
            return _apartmentID;
        }

        public override string ToString()
        {
            return "{Apartment ID=" + ApartmentID.ToString() + '}';
        }
    }

    public class LinkApartmentProxy : Link
    {
        public LinkApartmentProxy(int apartmentProxyID)
          : base(LinkTypeID.Service)
        {
            _apartmentProxyID = apartmentProxyID;
        }

        private readonly int _apartmentProxyID;
        public int ApartmentProxyID
        {
            get => _apartmentProxyID;
        }

        #region Link implementation

        public override int Size()
        {
            return 5;
        }

        public override void Write(MorphWriter writer)
        {
            writer.WriteLinkByte(LinkTypeID, true, true, false);
            writer.WriteInt32(_apartmentProxyID);
        }

        #endregion

        public override bool Equals(object obj)
        {
            return (obj is LinkApartmentProxy linkApartmentProxy) && (linkApartmentProxy._apartmentProxyID == _apartmentProxyID);
        }

        public override int GetHashCode()
        {
            return _apartmentProxyID;
        }

        public override string ToString()
        {
            return "{ApartmentProxy ID=" + ApartmentProxyID.ToString() + '}';
        }
    }

    public class LinkTypeService : ILinkTypeReader, ILinkTypeAction
    {
        public LinkTypeID ID
        {
            get => LinkTypeID.Service;
        }

        public Link ReadLink(MorphReader reader)
        {
            bool isApartment, isProxy, z;
            reader.ReadLinkByte(out isApartment, out isProxy, out z);
            if (isApartment)
                if (isProxy)
                    return new LinkApartmentProxy(reader.ReadInt32());
                else
                    return new LinkApartment(reader.ReadInt32());
            else
                return new LinkService(reader.ReadIdentifier());
        }

        protected virtual void ActionLinkService(LinkMessage message, LinkService linkService)
        {
            //  Obtain an apartment (create new or get shared)
            MorphApartment apartment = MorphServices.Obtain(linkService.ServiceName).ApartmentFactory.ObtainDefault();
            //  Replace the service link with an apartment link
            message.PathTo.Pop();
            message.PathTo.Push(new LinkApartment(apartment));
            //  Update the path to the client side
            if (apartment is MorphApartmentSession sessionApartment)
                sessionApartment.Path = message.PathFrom;
            //  Action the new link
            LinkTypes.ActionCurrentLink(message);
        }

        protected virtual void ActionLinkApartment(LinkMessage message, LinkApartment linkApartment)
        {
            //  Find the apartment
            MorphApartment apartment = MorphApartmentFactory.Obtain(linkApartment.ApartmentID);
            //  If it's a session apartment, then update the path
            if ((apartment is MorphApartmentSession sessionApartment) && message.HasPathFrom)
            {
                message.PathFrom.PeekAll();
                sessionApartment.Path = message.PathFrom.Clone();
            }
            //  Move along
            message.Context = apartment;
            message.NextLinkAction();
        }

        protected virtual void ActionLinkApartmentProxy(LinkMessage message, LinkApartmentProxy linkApartmentProxy)
        {
            //  Find the apartment proxy
            MorphApartmentProxy apartmentProxy = MorphApartmentProxy.Find(linkApartmentProxy.ApartmentProxyID);
            if (apartmentProxy == null)
                throw new EMorph("Apartment proxy not found");
            //  Move along
            message.Context = apartmentProxy;
            message.NextLinkAction();
        }

        public virtual void ActionLink(LinkMessage message, Link currentLink)
        {
            if (currentLink is LinkService) ActionLinkService(message, (LinkService)currentLink);
            if (currentLink is LinkApartment) ActionLinkApartment(message, (LinkApartment)currentLink);
            if (currentLink is LinkApartmentProxy) ActionLinkApartmentProxy(message, (LinkApartmentProxy)currentLink);
        }
    }
}