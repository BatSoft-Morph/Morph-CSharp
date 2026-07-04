using System.IO;
using System.Net;
using Morph.Core;

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
            string uri;
            if (hasURI)
                //  String
                uri = reader.ReadString();
            else
            { //  Binary
              //  Unfortunately one can't create an instance of IPAddress using short[8],
              //  so we create a string that IPAddress is able to parse.
                MorphWriter stream = new MorphWriter(new MemoryStream());
                int i = 0;
                do
                {
                    short value = (short)reader.ReadInt16();
                    stream.WriteInt16(value);
                    if (i == 8)
                        break;
                    stream.WriteString(":");
                } while (true);
                uri = stream.ToString();
            }
            //  Parse the address
            IPAddress address;
            try
            {
                address = IPAddress.Parse(uri);
            }
            catch
            {
                throw new EMorph("Invalid IPv6 Address");
            }
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
            size += 4 + MorphWriter.SizeOfString(EndPoint.Address.ToString());
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
            writer.WriteString(EndPoint.Address.ToString());
            // Port
            if (hasPort)
                writer.WriteInt16(EndPoint.Port);
        }

        #endregion
    }
}