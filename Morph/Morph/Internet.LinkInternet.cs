using Morph.Base;
using Morph.Core;
using System.Net;
using System.Net.Sockets;

namespace Morph.Internet
{
    public abstract class LinkInternet : Link
    {
        protected LinkInternet(IPEndPoint endPoint)
          : base(LinkTypeID.Internet)
        {
            _endPoint = endPoint;
        }

        static protected IPAddress URIToAddress(string uri, AddressFamily addressFamily)
        {
            IPAddress[] addresses = Dns.GetHostAddresses(uri);
            if (addresses.Length == 0)
                throw new EMorph("Invalid IP Address");
            foreach (IPAddress address in addresses)
                if (address.AddressFamily == addressFamily)
                    return address;
            throw new EMorph(EMorph.Any, $"Invalid {addressFamily} Address");
        }

        public const int MorphPort = 0x3000;

        private readonly IPEndPoint _endPoint;
        public IPEndPoint EndPoint
        {
            get => _endPoint;
        }

        static public LinkInternet New(EndPoint endPoint)
        {
            if (!(endPoint is IPEndPoint))
                throw new EMorphImplementation();
            if (endPoint.AddressFamily == AddressFamily.InterNetworkV6)
                return new LinkInternetIPv6((IPEndPoint)endPoint);
            else
                return new LinkInternetIPv4((IPEndPoint)endPoint);
        }

        public override bool Equals(object obj)
        {
            return (obj is LinkInternet linkInternet) && (linkInternet.EndPoint.Equals(_endPoint));
        }

        public override int GetHashCode()
        {
            return _endPoint.GetHashCode();
        }

        public override string ToString()
        {
            return "{Internet EndPoint=" + EndPoint.ToString() + '}';
        }
    }

    public class LinkTypeInternet : ILinkTypeReader, ILinkTypeAction
    {
        static public void Register()
        {
            LinkTypes.Register(new LinkTypeInternet());
        }

        public LinkTypeID ID
        {
            get => LinkTypeID.Internet;
        }

        public Link ReadLink(MorphReader reader)
        {
            bool isIPv6, isString, hasPort;
            reader.ReadLinkByte(out isIPv6, out isString, out hasPort);
            if (isIPv6)
                return LinkInternetIPv6.ReadNew(reader, isString, hasPort);
            else
                return LinkInternetIPv4.ReadNew(reader, isString, hasPort);
        }

        public void ActionLink(LinkMessage message, Link currentLink)
        {
            LinkInternet link = (LinkInternet)currentLink;
            //  If this link represents here, then move on to the next link
            if (Connections.IsEndPointOnThisProcess(link.EndPoint))
            {
                message.Context = link.EndPoint;
                message.NextLinkAction();
                return;
            }
            //  Obtain a connection to the device that link refers to.
            Connection connection;
            if (message.IsForceful)
                connection = Connections.Obtain(link.EndPoint);
            else
                connection = Connections.Find(link.EndPoint);
            //  If not forceful and connection not found, then stop the message here
            if (connection == null)
                return;
            //  Remove this address (this is done only in IPv4 due to NAT)
            if (link is LinkInternetIPv4)
                message.PathTo.Pop();
            else
              //  Ensure we have a link representing this device in Message.PathFrom
              if (message.HasPathFrom)
            {
                bool addLocalLink = false;
                Link lastLink = message.PathFrom.Peek();
                if (lastLink is LinkInternet)
                    addLocalLink = !Connections.IsEndPointOnThisProcess(((LinkInternet)lastLink).EndPoint);
                else
                    addLocalLink = true;
                if (addLocalLink)
                    message.PathFrom.Push(LinkInternet.New(connection.LocalEndPoint));
            }
            //  Send the message on
            connection.Write(message);
        }
    }
}