using Microsoft.Maui.Controls;
using Morph.Daemon.Client;
using Morph.Endpoint;
using Morph.Manager.ViewModels;
using System;

namespace Morph.Manager.Views
{
    public partial class ServicesPage : ContentPage
    {
        public ServicesPage()
        {
            InitializeComponent();
            _viewModel = new ServicesViewModel();
            _viewModel.Failed += ShowException;
            BindingContext = _viewModel;
            _viewModel.StartListening();
        }

        private readonly ServicesViewModel _viewModel;
        private bool _loaded;

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            if (_loaded)
                return;
            _loaded = true;
            await _viewModel.RefreshAsync();
        }

        private async void OnRefreshClicked(object sender, EventArgs args)
            => await _viewModel.RefreshAsync();

        //  Right-click a running service to register (or edit) its automatic startup.  The service
        //  name is dictated by the service, so it is locked; the rest is pre-filled from the existing
        //  startup when there is one, otherwise the user must browse to the hosting application.
        private async void OnAddToStartupsClicked(object sender, EventArgs args)
        {
            ServiceRow row = (sender as MenuFlyoutItem)?.BindingContext as ServiceRow;
            if (row == null)
                return;
            StartupEditPage editor = row.Startup.HasValue
                ? StartupEditPage.ForExisting(row.ServiceName, row.Startup.Value.fileName, row.Startup.Value.parameters, row.Startup.Value.timeout)
                : StartupEditPage.ForService(row.ServiceName);
            await Navigation.PushModalAsync(editor);
            StartupEditResult result = await editor.Result;
            if (result != null)
                await _viewModel.RegisterStartupAsync(result.ServiceName, result.FileName, result.Parameters, result.Timeout);
        }

        private async void ShowException(Exception x)
        {
            if (x is EMorphInvocation invocation)
                await DisplayAlertAsync(invocation.ClassName, x.Message, "OK");
            else if (x.InnerException == null)
                await DisplayAlertAsync(x.GetType().Name, x.Message, "OK");
            else
                await DisplayAlertAsync(x.GetType().Name, x.Message + '\n' + x.InnerException.Message, "OK");
        }
    }
}
