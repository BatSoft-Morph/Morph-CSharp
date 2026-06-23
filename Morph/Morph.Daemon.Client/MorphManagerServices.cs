using System;
using Morph.Endpoint;
using Morph.Params;
using Morph.Sequencing;

namespace Morph.Daemon.Client
{
    public class MorphManagerServices : DaemonClient
    {
        #region Internal

        internal MorphManagerServices(TimeSpan defaultTimeout)
          : base("Morph.Services", defaultTimeout)
        {
        }

        private MorphService RegisterLocalService(string serviceName, bool accessLocal, bool accessRemote, MorphApartmentFactory apartmentFactory)
        {
            MorphService service = MorphServices.Register(serviceName, apartmentFactory);
            //  Try to register the service with the Morph Daemon
            try
            { //  Tell the Morph daemon to redirect service requests to here
                ServletProxy.CallMethod("Start", new object[] { serviceName, accessLocal, accessRemote }, false);
                //  Done
                return service;
            }
            catch
            { //  If that fails, then tidy up
                MorphServices.Deregister(serviceName);
                throw;
            }
        }

        #endregion

        public MorphService StartServiceShared(string serviceName, bool accessLocal, bool accessRemote, object defaultObject, InstanceFactories instanceFactories)
        {
            MorphApartmentFactory apartmentFactory = new MorphApartmentFactoryShared(defaultObject, instanceFactories);
            return RegisterLocalService(serviceName, accessLocal, accessRemote, apartmentFactory);
        }

        public MorphService StartServiceSessioned(string serviceName, bool accessLocal, bool accessRemote, IDefaultServletObjectFactory defaultObjectFactory, InstanceFactories instanceFactories, TimeSpan timeout, SequenceLevel sequenceLevel)
        {
            MorphApartmentFactory apartmentFactory = new MorphApartmentFactorySession(defaultObjectFactory, instanceFactories, timeout, sequenceLevel);
            return RegisterLocalService(serviceName, accessLocal, accessRemote, apartmentFactory);
        }

        public MorphService StartServiceSessioned(string serviceName, bool accessLocal, bool accessRemote, MorphApartmentFactorySession apartmentFactory)
        {
            return RegisterLocalService(serviceName, accessLocal, accessRemote, apartmentFactory);
        }

        public void StopService(string serviceName)
        {
            try
            {
                ServletProxy.CallMethod("Stop", new object[] { serviceName }, false);
            }
            finally
            {
                MorphServices.Deregister(serviceName);
            }
        }

        public void StopService(MorphService service)
        {
            try
            {
                ServletProxy.CallMethod("Stop", new object[] { service.Name }, false);
            }
            finally
            {
                service.Deregister();
            }
        }

        public DaemonService[] ListServices()
        {
            return (DaemonService[])ServletProxy.CallMethod("ListServices", null, true);
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

    public struct DaemonService
    {
        public string serviceName;
        public bool accessLocal;
        public bool accessRemote;
    }
}