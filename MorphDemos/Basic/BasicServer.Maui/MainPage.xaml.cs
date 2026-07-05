using System;
using Basic;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;
using Morph.Daemon.Client;

namespace BasicServer.Maui
{
    public partial class MainPage : ContentPage, BasicUI
    {
        public MainPage()
        {
            InitializeComponent();
        }

        private bool _started = false;

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            if (_started)
                return;
            _started = true;
            try
            {
                MorphManager.Startup(2);
                MorphManager.Services.StartServiceShared(BasicInterface.ServiceName, true, true, new BasicDefaultImpl(this), new BasicFactories());
                StatusLabel.Text = "Service '" + BasicInterface.ServiceName + "' is running.  Waiting for a client.";
            }
            catch (Exception x)
            {
                StatusLabel.Text = "Could not start the service.";
                await DisplayAlertAsync("Morph", "Could not reach the Morph daemon.  Ensure Morph.Daemon is running.\n\n" + x.Message, "OK");
            }
        }

        #region BasicUI  (Morph invokes these on a background thread, so marshal to the UI thread)

        public int Number
        {
            get => int.Parse(OnUI(() => NumberEntry.Text ?? "0"));
            set => OnUI(() => { NumberEntry.Text = value.ToString(); return 0; });
        }

        public string Str
        {
            get => OnUI(() => TextEntry.Text ?? "");
            set => OnUI(() => { TextEntry.Text = value; return 0; });
        }

        private static T OnUI<T>(Func<T> func)
        {
            if (MainThread.IsMainThread)
                return func();
            return MainThread.InvokeOnMainThreadAsync(func).GetAwaiter().GetResult();
        }

        #endregion
    }
}
