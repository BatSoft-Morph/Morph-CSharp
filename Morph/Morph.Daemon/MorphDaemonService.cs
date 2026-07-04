using System;
using Bat.Library.Logging;
using Bat.Library.Service;
using Morph.Base;
using Morph.Core;
using Morph.Endpoint;
using Morph.Internet;
using Morph.Params;

namespace Morph.Daemon
{
    public class MorphDaemonService : WindowsService
    {
        public MorphDaemonService()
          : base()
        {
            ServiceName = InstallName;
            CanHandlePowerEvent = false;
            CanHandleSessionChangeEvent = false;
            CanPauseAndContinue = false;
            CanShutdown = true;
            CanStop = true;
            AutoLog = true;
            IsLogging = false;
        }

        public const string InstallName = "Morph.Daemon";
        public const string DisplayName = "Morph Daemon";
        public const string Description = "Connects applications that communicate via the Morph protocol.";

        /// <summary>Runs the daemon as a plain console process, for development and testing.</summary>
        public void RunConsole(string[] args)
        {
            DoStart(args);
            Console.WriteLine(DisplayName + " is running.  Press Enter to stop.");
            Console.ReadLine();
            DoStop();
        }

        public const int ThreadCount = 5;
        public const string ServiceName_ServicesStartup = "Morph.Startup";
        public const string ServiceName_Services = "Morph.Services";
        public const string ServiceName_Apartments = "Morph.Apartments";
        public const string ServiceName_ApartmentProxies = "Morph.ApartmentProxies";

        private void StartService(string ServiceName, object DefaultServlet, InstanceFactories Factory)
        {
            MorphApartmentFactory service = new MorphApartmentFactoryShared(DefaultServlet, Factory);
            //  Start the service (normally)
            MorphServices.Register(ServiceName, service);
            //  Register the service with the daemon (alongside services registered by other processes)
            RegisteredServices.ObtainByName(ServiceName).Running = new RegisteredRunningDaemon(RegisteredServices.ObtainByName(ServiceName));
            //  Register the service's shared apartment (alongside services registered by other processes)
            new RegisteredApartmentDaemon(RegisteredApartments.Apartments, service.ObtainDefault().ID);
            MorphApartmentProxy.IDFactory.Generate();
        }

        protected override void DoStart(string[] args)
        {
            try
            {
                //  Register link types        
                LinkTypes.Register(new LinkTypeEnd());
                LinkTypes.Register(new LinkTypeMessage());
                LinkTypes.Register(new LinkTypeData());
                LinkTypes.Register(new LinkTypeInternet());
                LinkTypes.RegisterReader(new LinkTypeService());
                LinkTypes.RegisterAction(new LinkTypeServiceDaemon());
                LinkTypes.Register(new LinkTypeServlet());
                LinkTypes.Register(new LinkTypeMember());
                //  Create instance factory
                DaemonFactory DaemonFactory = new DaemonFactory();
                InstanceFactories SimpleFactory = new InstanceFactories();
                //  Register the services
                ActionHandler.SetThreadCount(ThreadCount);
                StartService(ServiceName_ServicesStartup, new StartupImpl(), DaemonFactory);
                StartService(ServiceName_Services, new ServicesImpl(), DaemonFactory);
                StartService(ServiceName_Apartments, new ApartmentObjects("Apartments", MorphApartment.IDFactory, RegisteredApartments.Apartments), SimpleFactory);
                StartService(ServiceName_ApartmentProxies, new ApartmentObjects("ApartmentProxies", MorphApartmentProxy.IDFactory, RegisteredApartments.ApartmentProxies), SimpleFactory);
                ListenerManager.Obtain(LinkInternet.MorphPort).StartAll();
            }
            catch (Exception x)
            {
                Log.Default.Add(x);
            }
        }

        protected override int DoStop()
        {
            try
            {
                //  Deregister the service
                MorphServices.Deregister(ServiceName_ServicesStartup);
                MorphServices.Deregister(ServiceName_Services);
                MorphServices.Deregister(ServiceName_Apartments);
                MorphServices.Deregister(ServiceName_ApartmentProxies);
                ListenerManager.Find(LinkInternet.MorphPort).StopAll();
                ActionHandler.SetThreadCount(0);
                Connections.CloseAll();
                return 0;
            }
            catch (Exception x)
            {
                Log.Default.Add(x);
                return 1;
            }
        }
    }
}