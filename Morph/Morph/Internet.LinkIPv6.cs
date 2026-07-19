using Morph.Core;
using System.Net;
using System.Net.Sockets;

namespace Morph.Internet
{
    public class LinkInternetIPv6 : LinkInternet
    {
        public LinkInternetIPv6(IPEndPoint endPoint)
          : base(endPoint)
        {
        }

        static public LinkInternetIPv6 ReadNew(MorphReader reader, bool hasURI, bool hasPort)
        {
            //  Read host
            IPAddress address;
            if (hasURI)
                address = URIToAddress(reader.ReadIdentifier(), AddressFamily.InterNetworkV6);
            else
                address = new IPAddress(reader.ReadBytes(16));
            //  Read port
            int port = LinkInternet.MorphPort;
            if (hasPort)
                port = reader.ReadInt16() & 0x0000FFFF;
            //  Done
            return new LinkInternetIPv6(new IPEndPoint(address, port));
        }

        #region Link members

        public override int Size()
        {
            int size = 1;
            //  Host
            size += 2 + MorphWriter.SizeOfString(EndPoint.Address.ToString());
            //  Port
            if (EndPoint.Port != LinkInternet.MorphPort)
                size += 2;
            return size;
        }

        public override void Write(MorphWriter writer)
        {
            bool isIPv6 = true;
            bool isString = true;
            bool hasPort = EndPoint.Port != LinkInternet.MorphPort;
            //  Link byte
            writer.WriteLinkByte(LinkTypeID, isIPv6, isString, hasPort);
            //  Host
            writer.WriteIdentifier(EndPoint.Address.ToString());
            // Port
            if (hasPort)
                writer.WriteInt16(EndPoint.Port);
        }

        #endregion
    }
}