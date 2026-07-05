using System.IO;
using System.Net;
using Morph.Core;
using Morph.Internet;
using NUnit.Framework;

namespace Morph.Tests
{
    /// <summary>
    /// Wire round-trips for the Internet link (LinkType tab "Internet"):  binary IPv4/IPv6 hosts,
    /// string (URI) hosts resolved via URIToAddress, and the port field.
    /// </summary>
    [TestFixture]
    public class InternetLinkTests
    {
        #region Helpers

        private static byte[] Write(Link link)
        {
            MorphWriter writer = new MorphWriter(new MemoryStream());
            link.Write(writer);
            return writer.ToArray();
        }

        private static Link Read(byte[] bytes)
            => new LinkTypeInternet().ReadLink(new MorphReaderSized(bytes));

        /// <summary>Builds an Internet link byte stream with an explicit host, bypassing Write (which only emits binary IPv4 / string IPv6).</summary>
        private static byte[] BuildLink(bool isIPv6, bool isString, byte[] binaryHost, string stringHost)
        {
            MorphWriter writer = new MorphWriter(new MemoryStream());
            writer.WriteLinkByte(LinkTypeID.Internet, isIPv6, isString, false);
            if (isString)
                writer.WriteString(stringHost);
            else
                writer.WriteBytes(binaryHost);
            return writer.ToArray();
        }

        #endregion

        [Test]
        public void IPv4_Binary_RoundTrips()
        {
            IPEndPoint endPoint = new IPEndPoint(IPAddress.Parse("192.168.1.5"), LinkInternet.MorphPort);
            Link read = Read(Write(new LinkInternetIPv4(endPoint)));
            Assert.That(read, Is.InstanceOf<LinkInternetIPv4>());
            Assert.That(((LinkInternet)read).EndPoint, Is.EqualTo(endPoint));
        }

        [Test]
        public void IPv4_HighPort_IsNotSignExtended()
        {
            //  Port 60000 > 32767:  the old (short) cast turned it negative;  the mask keeps it unsigned
            IPEndPoint endPoint = new IPEndPoint(IPAddress.Parse("10.0.0.1"), 60000);
            Link read = Read(Write(new LinkInternetIPv4(endPoint)));
            Assert.That(((LinkInternet)read).EndPoint.Port, Is.EqualTo(60000));
        }

        [Test]
        public void IPv4_UriString_ResolvesLiteral()
        {
            //  A literal IP passes through Dns.GetHostAddresses without a network query
            byte[] bytes = BuildLink(isIPv6: false, isString: true, binaryHost: null, stringHost: "127.0.0.1");
            Link read = Read(bytes);
            Assert.That(read, Is.InstanceOf<LinkInternetIPv4>());
            Assert.That(((LinkInternet)read).EndPoint.Address, Is.EqualTo(IPAddress.Loopback));
        }

        [Test]
        public void IPv6_Binary_ReadsSixteenBytes()
        {
            //  Exercises the rewritten binary IPv6 reader (was an infinite loop / corrupt string before)
            byte[] bytes = BuildLink(isIPv6: true, isString: false, binaryHost: IPAddress.IPv6Loopback.GetAddressBytes(), stringHost: null);
            Link read = Read(bytes);
            Assert.That(read, Is.InstanceOf<LinkInternetIPv6>());
            Assert.That(((LinkInternet)read).EndPoint.Address, Is.EqualTo(IPAddress.IPv6Loopback));
        }

        [Test]
        public void IPv6_UriString_RoundTrips()
        {
            //  IPv6 Write always emits the string form, so this exercises URIToAddress on decode
            IPEndPoint endPoint = new IPEndPoint(IPAddress.IPv6Loopback, LinkInternet.MorphPort);
            Link read = Read(Write(new LinkInternetIPv6(endPoint)));
            Assert.That(read, Is.InstanceOf<LinkInternetIPv6>());
            Assert.That(((LinkInternet)read).EndPoint.Address, Is.EqualTo(IPAddress.IPv6Loopback));
        }

        [Test]
        public void IPv6_UriString_WrongFamily_Throws()
        {
            //  An IPv4 literal on an IPv6 link:  URIToAddress finds no InterNetworkV6 match and throws
            byte[] bytes = BuildLink(isIPv6: true, isString: true, binaryHost: null, stringHost: "127.0.0.1");
            Assert.That(() => Read(bytes), Throws.InstanceOf<EMorph>());
        }
    }
}
