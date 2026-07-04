using Microsoft.Maui.Controls;
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
