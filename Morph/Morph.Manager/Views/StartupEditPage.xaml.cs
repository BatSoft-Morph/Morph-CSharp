using Microsoft.Maui.Controls;
using Microsoft.Maui.Storage;
using System;
using System.IO;
using System.Threading.Tasks;

namespace Morph.Manager.Views
{
    /// <summary>The values chosen in the startup editor, or null if cancelled.</summary>
    public class StartupEditResult
    {
        public string ServiceName;
        public string FileName;
        public string Parameters;
        public int Timeout;
    }

    /// <summary>
    /// Modal editor for one startup registration.
    /// The service name identifies the startup, so it is locked when editing an existing one.
    /// </summary>
    public partial class StartupEditPage : ContentPage
    {
        private StartupEditPage(bool nameEditable)
        {
            InitializeComponent();
            entryServiceName.IsEnabled = nameEditable;
            stepperTimeout.Value = 10;
            ValidateValues();
        }

        static public StartupEditPage ForNew()
            => new StartupEditPage(true);

        static public StartupEditPage ForExisting(string serviceName, string fileName, string parameters, int timeout)
        {
            StartupEditPage page = new StartupEditPage(false);
            page.entryServiceName.Text = serviceName;
            page.entryFileName.Text = fileName;
            page.entryParameters.Text = parameters;
            page.stepperTimeout.Value = timeout;
            page.ValidateValues();
            return page;
        }

        /// <summary>
        /// A new startup for a running service:  the service name is dictated by the service (so it
        /// is locked and pre-filled), but the application file and the rest are not known - a running
        /// service does not record what hosts it - so the user must fill them in.
        /// </summary>
        static public StartupEditPage ForService(string serviceName)
        {
            StartupEditPage page = new StartupEditPage(false);
            page.entryServiceName.Text = serviceName;
            page.ValidateValues();
            return page;
        }

        private readonly TaskCompletionSource<StartupEditResult> _result = new TaskCompletionSource<StartupEditResult>();

        /// <summary>Completes when the page closes:  the chosen values, or null if cancelled.</summary>
        public Task<StartupEditResult> Result
        {
            get => _result.Task;
        }

        #region Validation

        //  OK requires a service name and an application file that exists
        private void ValidateValues()
        {
            butOK.IsEnabled =
                !string.IsNullOrEmpty(entryServiceName.Text) &&
                File.Exists(entryFileName.Text);
        }

        private void OnAnythingChanged(object sender, TextChangedEventArgs args)
            => ValidateValues();

        private void OnTimeoutChanged(object sender, ValueChangedEventArgs args)
            => labelTimeout.Text = ((int)args.NewValue).ToString();

        #endregion

        private async void OnBrowseClicked(object sender, EventArgs args)
        {
            FileResult picked = await FilePicker.Default.PickAsync(new PickOptions
            {
                PickerTitle = "Choose the application that hosts the service",
            });
            if (picked != null)
                entryFileName.Text = picked.FullPath;
        }

        private async void OnOKClicked(object sender, EventArgs args)
        {
            //  The result must be set before popping:  popping raises OnDisappearing,
            //  whose cancelled-result safety net would otherwise win the race.
            _result.TrySetResult(new StartupEditResult
            {
                ServiceName = entryServiceName.Text,
                FileName = entryFileName.Text,
                Parameters = entryParameters.Text ?? string.Empty,
                Timeout = (int)stepperTimeout.Value,
            });
            await Navigation.PopModalAsync();
        }

        private async void OnCancelClicked(object sender, EventArgs args)
        {
            _result.TrySetResult(null);
            await Navigation.PopModalAsync();
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            //  Covers closing the modal by other means (e.g. the back gesture)
            _result.TrySetResult(null);
        }
    }
}
