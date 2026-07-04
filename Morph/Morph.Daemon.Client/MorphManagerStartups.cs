using System;

namespace Morph.Daemon.Client
{
    public class MorphManagerStartups : DaemonClient
    {
        public MorphManagerStartups(TimeSpan defaultTimeout)
          : base("Morph.Startup", defaultTimeout)
        {
        }

        /**
         * Timeout is in seconds
         */
        public void Add(string serviceName, string fileName, string parameters, int timeout)
        {
            ServletProxy.CallMethod("Add", new object[] { serviceName, fileName, parameters, timeout }, false);
        }

        public void Remove(string serviceName)
        {
            ServletProxy.CallMethod("Remove", new object[] { serviceName }, false);
        }

        public DaemonStartup[] ListServices()
        {
            return (DaemonStartup[])ServletProxy.CallMethod("ListServices", null, true);
        }

        public void Listen(DaemonServiceCallback callback)
        {
            ServletProxy.CallMethod("Listen", new object[] { callback }, false);
        }

        public void Unlisten(DaemonServiceCallback callback)
        {
            ServletProxy.CallMethod("Unlisten", new object[] { callback }, false);
        }
    }

    public struct DaemonStartup
    {
        public string serviceName;
        public string fileName;
        public int timeout;
    }
}