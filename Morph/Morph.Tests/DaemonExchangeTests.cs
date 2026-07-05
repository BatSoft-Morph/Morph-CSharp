using Morph.Base;
using Morph.Core;
using Morph.Daemon.Client;
using Morph.Params;
using NUnit.Framework;

namespace Morph.Tests
{
    /// <summary>
    /// Round-trips the exact payloads the Morph Manager exchanges with the daemon,
    /// using the same instance factory configuration as DaemonClient.
    /// </summary>
    [TestFixture]
    public class DaemonExchangeTests
    {
        private static object RoundTripSpecial(object special)
        {
            InstanceFactories factories = DaemonClient.InstanceFactory;
            MorphWriter writer = Parameters.Encode(null, special, factories);
            MorphReader reader = new LinkData(writer).Reader;
            Parameters.Decode(factories, null, reader, out object[] _, out object result);
            return result;
        }

        [Test]
        public void DaemonStartupArray_RoundTripsTyped()
        {
            DaemonStartup[] startups = new DaemonStartup[]
            {
                new DaemonStartup { serviceName = "ServiceA", fileName = @"C:\a.exe", parameters = "-a", timeout = 10 },
                new DaemonStartup { serviceName = "ServiceB", fileName = @"C:\b.exe", parameters = "", timeout = 20 },
            };
            object result = RoundTripSpecial(startups);
            Assert.That(result, Is.InstanceOf<DaemonStartup[]>());
            DaemonStartup[] typed = (DaemonStartup[])result;
            Assert.That(typed.Length, Is.EqualTo(2));
            Assert.That(typed[0].serviceName, Is.EqualTo("ServiceA"));
            Assert.That(typed[0].fileName, Is.EqualTo(@"C:\a.exe"));
            Assert.That(typed[0].parameters, Is.EqualTo("-a"));
            Assert.That(typed[0].timeout, Is.EqualTo(10));
            Assert.That(typed[1].serviceName, Is.EqualTo("ServiceB"));
            Assert.That(typed[1].parameters, Is.EqualTo(""));
        }

        [Test]
        public void EmptyDaemonStartupArray_RoundTripsTyped()
        {
            object result = RoundTripSpecial(new DaemonStartup[0]);
            Assert.That(result, Is.InstanceOf<DaemonStartup[]>());
            Assert.That(((DaemonStartup[])result).Length, Is.EqualTo(0));
        }

        [Test]
        public void DaemonServiceArray_RoundTripsTyped()
        {
            DaemonService[] services = new DaemonService[]
            {
                new DaemonService { serviceName = "Morph.Startup", accessLocal = true, accessRemote = false },
            };
            object result = RoundTripSpecial(services);
            Assert.That(result, Is.InstanceOf<DaemonService[]>());
            DaemonService[] typed = (DaemonService[])result;
            Assert.That(typed[0].serviceName, Is.EqualTo("Morph.Startup"));
            Assert.That(typed[0].accessLocal, Is.True);
            Assert.That(typed[0].accessRemote, Is.False);
        }

        [Test]
        public void AddStartupParams_RoundTrip()
        {
            //  The exact parameter list of MorphManagerStartups.Add
            InstanceFactories factories = DaemonClient.InstanceFactory;
            MorphWriter writer = Parameters.Encode(new object[] { "MyService", @"C:\my.exe", "-run", 10 }, factories);
            MorphReader reader = new LinkData(writer).Reader;
            Parameters.Decode(factories, null, reader, out object[] Params);
            Assert.That(Params, Is.EqualTo(new object[] { "MyService", @"C:\my.exe", "-run", 10 }));
        }
    }
}
