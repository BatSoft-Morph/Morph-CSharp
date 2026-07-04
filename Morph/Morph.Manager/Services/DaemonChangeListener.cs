using Microsoft.Maui.ApplicationModel;
using Morph.Daemon.Client;
using Morph.Endpoint;
using System;

namespace Morph.Manager.Services
{
    /// <summary>
    /// Receives Added/Removed callbacks from the Morph Daemon and
    /// forwards them to the UI thread.
    /// </summary>
    internal class DaemonChangeListener : DaemonServiceCallback
    {
        static DaemonChangeListener()
        {
            s_apartment = new MorphApartmentShared(DaemonClient.InstanceFactory);
        }

        public DaemonChangeListener(Action changed)
        {
            _changed = changed;
            MorphApartment = s_apartment;
        }

        private static readonly MorphApartment s_apartment;
        private readonly Action _changed;

        public override void Added(string serviceName)
            => MainThread.BeginInvokeOnMainThread(_changed);

        public override void Removed(string serviceName)
            => MainThread.BeginInvokeOnMainThread(_changed);
    }
}
