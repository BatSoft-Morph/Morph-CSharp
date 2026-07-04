using Morph.Daemon.Client;
using Morph.Manager.Services;
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace Morph.Manager.ViewModels
{
    /// <summary>The running Morph services, as reported by the daemon.</summary>
    public class ServicesViewModel : ViewModelBase
    {
        public ObservableCollection<ServiceRow> Services { get; } = new ObservableCollection<ServiceRow>();

        private bool _isRefreshing;
        public bool IsRefreshing
        {
            get => _isRefreshing;
            private set
            {
                _isRefreshing = value;
                RaisePropertyChanged();
            }
        }

        /// <summary>Subscribes to daemon change notifications.  Safe to call when the daemon is down.</summary>
        public void StartListening()
        {
            try
            {
                MorphManager.Services.Listen(new DaemonChangeListener(() => _ = RefreshAsync()));
            }
            catch (Exception x)
            {
                ReportFailure(x);
            }
        }

        public async Task RefreshAsync()
        {
            if (IsRefreshing)
                return;
            IsRefreshing = true;
            try
            {
                DaemonService[] services = await Task.Run(() => MorphManager.Services.ListServices());
                Services.Clear();
                if (services != null)
                    foreach (DaemonService service in services)
                        Services.Add(new ServiceRow(service));
            }
            catch (Exception x)
            {
                ReportFailure(x);
            }
            finally
            {
                IsRefreshing = false;
            }
        }
    }

    public class ServiceRow
    {
        public ServiceRow(DaemonService service)
        {
            ServiceName = service.serviceName;
            AccessLocal = service.accessLocal ? "Yes" : "No";
            AccessRemote = service.accessRemote ? "Yes" : "No";
        }

        public string ServiceName { get; }
        public string AccessLocal { get; }
        public string AccessRemote { get; }
    }
}
