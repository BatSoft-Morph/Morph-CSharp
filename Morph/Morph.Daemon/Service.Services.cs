using System.Collections.Generic;
using Morph.Base;
using Morph.Internet;
using Morph.Params;

namespace Morph.Daemon
{
    public class ServicesImpl : IMorphParameters
    {
        static internal ServiceCallbacks _ServiceCallbacks = new ServiceCallbacks();

        public void Start(LinkMessage message, string serviceName, bool accessLocal, bool accessRemote)
        {
            //  Register the service with the daemon
            RegisteredService service = RegisteredServices.ObtainByName(serviceName);
            lock (service)
                if (message is LinkMessageFromIP)
                {
                    RegisteredRunningInternet running = new RegisteredRunningInternet(service, ((LinkMessageFromIP)message).Connection);
                    running.AccessLocal = accessLocal;
                    running.AccessRemote = accessRemote;
                    try
                    {
                        service.Running = running;
                    }
                    catch
                    { //  The setter rejected it (e.g. already running), so release its connection subscription
                        running.Dispose();
                        throw;
                    }
                }
                else
                    throw new EMorphDaemon(GetType().Name + ".Start(): Unhandled message type \"" + message.GetType().Name + "\".");
        }

        public void Stop(LinkMessage message, string serviceName)
        {
            //  Find the service
            RegisteredService service = RegisteredServices.FindByName(serviceName);
            if (service == null)
                return; //  Nothing to find
                        //  Stop service from IP
            if (message is LinkMessageFromIP)
                lock (service)
                {
                    Connection connection = ((LinkMessageFromIP)message).Connection;
                    if (service.IsRunning)
                        if (!(service.Running is RegisteredRunningInternet) || (((RegisteredRunningInternet)service.Running).Connection != connection))
                            throw new EMorphDaemon("Caller cannot stop a service " + serviceName + " which it does not own.");
                    service.Running = null;
                    return;
                }
            //  This should not happen with a full implementation
            throw new EMorphDaemon(MorphDaemonService.ServiceName_Services + ".Stop(): \"" + serviceName + "\"");
        }

        public DaemonService[] ListServices(LinkMessage message)
        {
            //  List services
            RegisteredService[] allServices = RegisteredServices.ListAll();
            //  Filter in running services
            List<DaemonService> result = new List<DaemonService>();
            for (int i = 0; i < allServices.Length; i++)
            {
                RegisteredService service = allServices[i];
                lock (service)
                    if (service.IsRunning)
                    {
                        DaemonService RunningService = new DaemonService();
                        RunningService.serviceName = service.Name;
                        RunningService.accessLocal = service.Running.AccessLocal;
                        RunningService.accessRemote = service.Running.AccessRemote;
                        result.Add(RunningService);
                    }
            }
            return result.ToArray();
        }

        public void Listen(LinkMessage message, ServiceCallback callback)
        {
            _ServiceCallbacks.Listen(callback);
        }

        public void Unlisten(LinkMessage message, ServiceCallback callback)
        {
            _ServiceCallbacks.Removed(callback);
        }
    }

    public struct DaemonService
    {
        public string serviceName;
        public bool accessLocal;
        public bool accessRemote;
    }
}