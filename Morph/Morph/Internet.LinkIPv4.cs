using Morph.Core;
using System.Net;
using System.Net.Sockets;

namespace Morph.Internet
{
    public class LinkInternetIPv4 : LinkInternet
    {
        public LinkInternetIPv4(IPEndPoint endPoint)
          : base(endPoint)
        {
        }

        static public LinkInternetIPv4 ReadNew(MorphReader reader, bool hasURI, bool hasPort)
        {
            //  Read host
            IPAddress address;
            if (hasURI)
                address = URIToAddress(reader.ReadIdentifier(), AddressFamily.InterNetwork);
            else
                address = new IPAddress(reader.ReadBytes(4));
            //  Read port
            int port = LinkInternet.MorphPort;
            if (hasPort)
                port = reader.ReadInt16() & 0x0000FFFF;
            //  Done
            return new LinkInternetIPv4(new IPEndPoint(address, port));
        }

        #region Link members

        //  An IPv4 host is always written in its 4 byte binary form;  a URI host is only ever read.
        public override int Size()
        {
            bool hasPort = EndPoint.Port != LinkInternet.MorphPort;
            return 5 + (hasPort ? 2 : 0);
        }

        public override void Write(MorphWriter writer)
        {
            bool isIPv6 = false;
            bool isString = false;
            bool hasPort = EndPoint.Port != LinkInternet.MorphPort;
            //  Link byte
            writer.WriteLinkByte(LinkTypeID, isIPv6, isString, hasPort);
            //  Host
            writer.WriteBytes(EndPoint.Address.GetAddressBytes());
            // Port
            if (hasPort)
                writer.WriteInt16(EndPoint.Port);
        }

        #endregion
    }
}