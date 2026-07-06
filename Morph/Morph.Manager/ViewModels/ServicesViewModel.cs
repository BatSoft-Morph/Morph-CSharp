using Microsoft.Maui.Controls;
using Morph.Daemon.Client;
using Morph.Manager.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;

namespace Morph.Manager.ViewModels
{
    /// <summary>The running Morph services, as reported by the daemon.</summary>
    public class ServicesViewModel : ViewModelBase
    {
        public ServicesViewModel()
        {
            SortCommand = new Command<string>(OnSortBy);
        }

        public ObservableCollection<ServiceRow> Services { get; } = new ObservableCollection<ServiceRow>();

        #region Sorting

        private readonly ListSorter<ServiceRow> _sorter = new ListSorter<ServiceRow>(
            "Service",
            new SortColumn<ServiceRow>("Service", "Service", (left, right) => CompareText(left.ServiceName, right.ServiceName)),
            new SortColumn<ServiceRow>("Local", "Local", (left, right) => left.AccessLocal.CompareTo(right.AccessLocal)),
            new SortColumn<ServiceRow>("Remote", "Remote", (left, right) => left.AccessRemote.CompareTo(right.AccessRemote)));

        public ICommand SortCommand { get; }

        public string ServiceHeader
        {
            get => _sorter.Caption("Service");
        }

        public string LocalHeader
        {
            get => _sorter.Caption("Local");
        }

        public string RemoteHeader
        {
            get => _sorter.Caption("Remote");
        }

        private static int CompareText(string left, string right)
            => string.Compare(left, right, StringComparison.CurrentCultureIgnoreCase);

        private void OnSortBy(string columnId)
        {
            _sorter.SortBy(columnId);
            ShowSorted(Services);
            RaisePropertyChanged(nameof(ServiceHeader));
            RaisePropertyChanged(nameof(LocalHeader));
            RaisePropertyChanged(nameof(RemoteHeader));
        }

        /// <summary>Rebuilds the collection in the current sort order.</summary>
        private void ShowSorted(IEnumerable<ServiceRow> rows)
        {
            List<ServiceRow> ordered = new List<ServiceRow>(rows);
            _sorter.Sort(ordered);
            Services.Clear();
            foreach (ServiceRow row in ordered)
                Services.Add(row);
        }

        #endregion

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
                Dictionary<string, DaemonStartup> startupsByName = await LoadStartupsByNameAsync();
                List<ServiceRow> loaded = new List<ServiceRow>();
                if (services != null)
                    foreach (DaemonService service in services)
                    {
                        DaemonStartup startup;
                        bool isStartup = startupsByName.TryGetValue(service.serviceName, out startup);
                        loaded.Add(new ServiceRow(service, isStartup ? startup : (DaemonStartup?)null));
                    }
                ShowSorted(loaded);
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

        //  The startup registrations only decorate each row's context menu (Add vs Edit, with
        //  prefill).  They are best-effort: if the Startup service can't be read, every row simply
        //  offers "Add" and the running-services list still stands on its own.
        private async Task<Dictionary<string, DaemonStartup>> LoadStartupsByNameAsync()
        {
            Dictionary<string, DaemonStartup> byName = new Dictionary<string, DaemonStartup>();
            try
            {
                DaemonStartup[] startups = await Task.Run(() => MorphManager.Startups.ListServices());
                if (startups != null)
                    foreach (DaemonStartup startup in startups)
                        byName[startup.serviceName] = startup;
            }
            catch
            {
                //  Optional decoration only - swallow so a missing Startup service never hides services.
            }
            return byName;
        }

        /// <summary>Registers (or replaces) the automatic startup for a service, through the daemon.</summary>
        public async Task RegisterStartupAsync(string serviceName, string fileName, string parameters, int timeout)
        {
            try
            {
                await Task.Run(() => MorphManager.Startups.Add(serviceName, fileName, parameters, timeout));
            }
            catch (Exception x)
            {
                ReportFailure(x);
            }
            //  Refresh so the just-registered row's context menu flips to "Edit startup".
            await RefreshAsync();
        }
    }

    public class ServiceRow
    {
        public ServiceRow(DaemonService service, DaemonStartup? startup)
        {
            ServiceName = service.serviceName;
            AccessLocal = service.accessLocal;
            AccessRemote = service.accessRemote;
            Startup = startup;
        }

        public string ServiceName { get; }
        public bool AccessLocal { get; }
        public bool AccessRemote { get; }

        /// <summary>The matching startup registration, when this service is set to start automatically.</summary>
        public DaemonStartup? Startup { get; }

        public string StartupMenuText
        {
            get => Startup.HasValue ? "Edit startup…" : "Add to startups…";
        }
    }
}
