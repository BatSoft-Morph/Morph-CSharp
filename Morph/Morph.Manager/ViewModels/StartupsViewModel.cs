using Microsoft.Maui.Controls;
using Morph.Daemon.Client;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;

namespace Morph.Manager.ViewModels
{
    /// <summary>
    /// The services the daemon starts automatically.  The daemon owns and persists this list;
    /// this view-model is a thin UI over the daemon's "Morph.Startup" service - it reads the list
    /// from the daemon and adds/removes through it, holding no file of its own.
    /// </summary>
    public class StartupsViewModel : ViewModelBase
    {
        public StartupsViewModel()
        {
            SortCommand = new Command<string>(OnSortBy);
        }

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

        #region Sorting

        private readonly ListSorter<StartupRow> _sorter = new ListSorter<StartupRow>(
            "Service",
            new SortColumn<StartupRow>("Service", "Service", (left, right) => CompareText(left.ServiceName, right.ServiceName)),
            new SortColumn<StartupRow>("Timeout", "Timeout (s)", (left, right) => left.Timeout.CompareTo(right.Timeout)),
            new SortColumn<StartupRow>("Application", "Application", (left, right) => CompareText(left.FileName, right.FileName)));

        public ICommand SortCommand { get; }

        public string ServiceHeader
        {
            get => _sorter.Caption("Service");
        }

        public string TimeoutHeader
        {
            get => _sorter.Caption("Timeout");
        }

        public string ApplicationHeader
        {
            get => _sorter.Caption("Application");
        }

        private static int CompareText(string left, string right)
            => string.Compare(left, right, StringComparison.CurrentCultureIgnoreCase);

        private void OnSortBy(string columnId)
        {
            _sorter.SortBy(columnId);
            ShowSorted(Startups);
            RaisePropertyChanged(nameof(ServiceHeader));
            RaisePropertyChanged(nameof(TimeoutHeader));
            RaisePropertyChanged(nameof(ApplicationHeader));
        }

        /// <summary>Rebuilds the collection in the current sort order, keeping the selection.</summary>
        private void ShowSorted(IEnumerable<StartupRow> rows)
        {
            string selectedServiceName = SelectedStartup?.ServiceName;
            List<StartupRow> ordered = new List<StartupRow>(rows);
            _sorter.Sort(ordered);
            Startups.Clear();
            StartupRow toSelect = null;
            foreach (StartupRow row in ordered)
            {
                Startups.Add(row);
                if (row.ServiceName == selectedServiceName)
                    toSelect = row;
            }
            SelectedStartup = toSelect;
        }

        #endregion

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
                List<StartupRow> loaded = new List<StartupRow>();
                if (startups != null)
                    foreach (DaemonStartup startup in startups)
                        loaded.Add(new StartupRow(startup));
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
