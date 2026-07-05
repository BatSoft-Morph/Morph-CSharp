using System.Collections.Generic;
using Morph.Endpoint;

namespace Morph.Daemon
{
    public class ServiceCallback
    {
        internal ServiceCallback(ServletProxy servletProxy)
        {
            _servletProxy = servletProxy;
        }

        private readonly ServletProxy _servletProxy;

        public void Added(string serviceName)
        {
            _servletProxy.SendMethod("Added", new object[] { serviceName });
        }

        public void Removed(string serviceName)
        {
            _servletProxy.SendMethod("Removed", new object[] { serviceName });
        }
    }

    public class ServiceCallbacks
    {
        private readonly List<ServiceCallback> _callbacks = new List<ServiceCallback>();

        public void DoCallbackAdded(string serviceName)
        {
            //  Invoke outside the lock:  each callback does a blocking network send that must not hold the lock
            foreach (ServiceCallback callback in Snapshot())
                try
                {
                    callback.Added(serviceName);
                }
                catch
                {
                    Removed(callback);
                }
        }

        public void DoCallbackRemoved(string serviceName)
        {
            //  Invoke outside the lock:  each callback does a blocking network send that must not hold the lock
            foreach (ServiceCallback callback in Snapshot())
                try
                {
                    callback.Removed(serviceName);
                }
                catch
                {
                    Removed(callback);
                }
        }

        private ServiceCallback[] Snapshot()
        {
            lock (_callbacks)
                return _callbacks.ToArray();
        }

        public void Listen(ServiceCallback Callback)
        {
            lock (_callbacks)
                _callbacks.Add(Callback);
        }

        public void Removed(ServiceCallback Callback)
        {
            lock (_callbacks)
                _callbacks.Remove(Callback);
        }
    }
}