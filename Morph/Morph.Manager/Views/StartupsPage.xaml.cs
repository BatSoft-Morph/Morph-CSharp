using Microsoft.Maui.Controls;
using Morph.Endpoint;
using Morph.Manager.ViewModels;
using System;

namespace Morph.Manager.Views
{
    public partial class StartupsPage : ContentPage
    {
        public StartupsPage()
        {
            InitializeComponent();
            _viewModel = new StartupsViewModel();
            _viewModel.Failed += ShowException;
            BindingContext = _viewModel;
        }

        private readonly StartupsViewModel _viewModel;
        private bool _loaded;

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            if (_loaded)
                return;
            _loaded = true;
            //  The stored list is already showing;  bring the daemon up to date with it
            await _viewModel.SyncWithDaemonAsync();
        }

        private async void OnRefreshClicked(object sender, EventArgs args)
            => await _viewModel.SyncWithDaemonAsync();

        private async void OnAddClicked(object sender, EventArgs args)
        {
            StartupEditPage editor = StartupEditPage.ForNew();
            await Navigation.PushModalAsync(editor);
            StartupEditResult result = await editor.Result;
            if (result != null)
                await _viewModel.AddAsync(result.ServiceName, result.FileName, result.Parameters, result.Timeout);
        }

        private async void OnEditClicked(object sender, EventArgs args)
        {
            StartupRow selected = _viewModel.SelectedStartup;
            if (selected == null)
                return;
            StartupEditPage editor = StartupEditPage.ForExisting(selected.ServiceName, selected.FileName, selected.Parameters, selected.Timeout);
            await Navigation.PushModalAsync(editor);
            StartupEditResult result = await editor.Result;
            if (result != null)
                await _viewModel.ReplaceAsync(result.ServiceName, result.FileName, result.Parameters, result.Timeout);
        }

        private async void OnRemoveClicked(object sender, EventArgs args)
        {
            StartupRow selected = _viewModel.SelectedStartup;
            if (selected == null)
                return;
            bool confirmed = await DisplayAlert(
                "Removing startup",
                "Are you sure you want to remove automatic startup of service \"" + selected.ServiceName + "\"?",
                "Yes", "No");
            if (confirmed)
                await _viewModel.RemoveAsync(selected.ServiceName);
        }

        private async void ShowException(Exception x)
        {
            if (x is EMorphInvocation invocation)
                await DisplayAlert(invocation.ClassName, x.Message, "OK");
            else if (x.InnerException == null)
                await DisplayAlert(x.GetType().Name, x.Message, "OK");
            else
                await DisplayAlert(x.GetType().Name, x.Message + '\n' + x.InnerException.Message, "OK");
        }
    }
}
