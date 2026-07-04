using Morph.Daemon.Client;
using Morph.Manager.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace Morph.Manager.ViewModels
{
    /// <summary>
    /// <para>The services to start automatically, and their startup settings.</para>
    /// <para>The persistent list lives in "Morph.Manager.json" beside the Morph Manager exe;
    /// the daemon is synchronised to it, best effort, so registering always succeeds locally
    /// even when the daemon is unreachable.</para>
    /// </summary>
    public class StartupsViewModel : ViewModelBase
    {
        public StartupsViewModel()
        {
            _entries = _store.Load();
            foreach (StartupEntry entry in _entries)
                Startups.Add(new StartupRow(entry));
        }

        private readonly StartupStore _store = new StartupStore();
        private readonly List<StartupEntry> _entries;

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

        #region The store is the truth

        private StartupEntry FindEntry(string serviceName)
        {
            foreach (StartupEntry entry in _entries)
                if (entry.ServiceName == serviceName)
                    return entry;
            return null;
        }

        private void ReloadRows()
        {
            string selectedServiceName = SelectedStartup?.ServiceName;
            Startups.Clear();
            foreach (StartupEntry entry in _entries)
            {
                StartupRow row = new StartupRow(entry);
                Startups.Add(row);
                if (entry.ServiceName == selectedServiceName)
                    SelectedStartup = row;
            }
        }

        public async Task AddAsync(string serviceName, string fileName, string parameters, int timeout)
        {
            StartupEntry entry = FindEntry(serviceName) ?? NewEntry(serviceName);
            entry.FileName = fileName;
            entry.Parameters = parameters;
            entry.Timeout = timeout;
            _store.Save(_entries);
            ReloadRows();
            await PushToDaemonAsync(entry);
        }

        public Task ReplaceAsync(string serviceName, string fileName, string parameters, int timeout)
            => AddAsync(serviceName, fileName, parameters, timeout);

        public async Task RemoveAsync(string serviceName)
        {
            StartupEntry entry = FindEntry(serviceName);
            if (entry == null)
                return;
            _entries.Remove(entry);
            _store.Save(_entries);
            ReloadRows();
            try
            {
                await Task.Run(() => MorphManager.Startups.Remove(serviceName));
            }
            catch (Exception x)
            {
                ReportFailure(x);
            }
        }

        private StartupEntry NewEntry(string serviceName)
        {
            StartupEntry entry = new StartupEntry { ServiceName = serviceName };
            _entries.Add(entry);
            return entry;
        }

        #endregion

        #region Best effort daemon synchronisation

        private async Task PushToDaemonAsync(StartupEntry entry)
        {
            try
            {
                await Task.Run(() => MorphManager.Startups.Add(entry.ServiceName, entry.FileName, entry.Parameters, entry.Timeout));
            }
            catch (Exception x)
            {
                ReportFailure(x);
            }
        }

        /// <summary>Pushes the whole stored list to the daemon.  One failure report covers the lot.</summary>
        public async Task SyncWithDaemonAsync()
        {
            if (IsRefreshing)
                return;
            IsRefreshing = true;
            try
            {
                await Task.Run(() =>
                {
                    foreach (StartupEntry entry in _entries)
                        MorphManager.Startups.Add(entry.ServiceName, entry.FileName, entry.Parameters, entry.Timeout);
                });
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

        #endregion
    }

    public class StartupRow
    {
        public StartupRow(StartupEntry entry)
        {
            ServiceName = entry.ServiceName;
            FileName = entry.FileName;
            Timeout = entry.Timeout;
            Parameters = entry.Parameters;
        }

        public string ServiceName { get; }
        public string FileName { get; }
        public string Parameters { get; }
        public int Timeout { get; }
    }
}
