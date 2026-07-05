using Morph.Daemon.Client;
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace Morph.Manager.ViewModels
{
    /// <summary>
    /// The services the daemon starts automatically.  The daemon owns and persists this list;
    /// this view-model is a thin UI over the daemon's "Morph.Startup" service - it reads the list
    /// from the daemon and adds/removes through it, holding no file of its own.
    /// </summary>
    public class StartupsViewModel : ViewModelBase
    {
        public ObservableCollection<StartupRow> Startups { get; } = new ObservableCollection<StartupRow>();

        private StartupRow _selectedStartup;
        public StartupRow SelectedStartup
        {
            get => _selectedStartup;
            set
            {
                _selectedStartup = value;
                RaisePropertyChanged();
                RaisePropertyChanged(nameof(HasSelection));
            }
        }

        public bool HasSelection
        {
            get => _selectedStartup != null;
        }

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

        #region The daemon is the truth

        /// <summary>Reloads the startup list from the daemon, which owns it.</summary>
        public async Task RefreshAsync()
        {
            if (IsRefreshing)
                return;
            IsRefreshing = true;
            try
            {
                DaemonStartup[] startups = await Task.Run(() => MorphManager.Startups.ListServices());
                string selectedServiceName = SelectedStartup?.ServiceName;
                Startups.Clear();
                if (startups != null)
                    foreach (DaemonStartup startup in startups)
                    {
                        StartupRow row = new StartupRow(startup);
                        Startups.Add(row);
                        if (startup.serviceName == selectedServiceName)
                            SelectedStartup = row;
                    }
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

        public async Task AddAsync(string serviceName, string fileName, string parameters, int timeout)
        {
            try
            {
                await Task.Run(() => MorphManager.Startups.Add(serviceName, fileName, parameters, timeout));
            }
            catch (Exception x)
            {
                ReportFailure(x);
            }
            await RefreshAsync();
        }

        public Task ReplaceAsync(string serviceName, string fileName, string parameters, int timeout)
            => AddAsync(serviceName, fileName, parameters, timeout);

        public async Task RemoveAsync(string serviceName)
        {
            try
            {
                await Task.Run(() => MorphManager.Startups.Remove(serviceName));
            }
            catch (Exception x)
            {
                ReportFailure(x);
            }
            await RefreshAsync();
        }

        #endregion
    }

    public class StartupRow
    {
        public StartupRow(DaemonStartup startup)
        {
            ServiceName = startup.serviceName;
            FileName = startup.fileName;
            Parameters = startup.parameters;
            Timeout = startup.timeout;
        }

        public string ServiceName { get; }
        public string FileName { get; }
        public string Parameters { get; }
        public int Timeout { get; }
    }
}
