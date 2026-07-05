using System;
using System.Threading.Tasks;
using Basic;
using Microsoft.Maui.Controls;
using Morph;
using Morph.Endpoint;

namespace BasicClient.Maui
{
    public partial class MainPage : ContentPage
    {
        public MainPage()
        {
            InitializeComponent();
        }

        private BasicDefault _basic;

        #region Value fields  (read/written on the UI thread only)

        private int FormNumber
        {
            get => int.Parse(NumberEntry.Text);
            set => NumberEntry.Text = value.ToString();
        }

        private string FormText
        {
            get => TextEntry.Text ?? "";
            set => TextEntry.Text = value;
        }

        #endregion

        #region Connect

        private async void OnConnect(object sender, EventArgs e)
        {
            string host = HostEntry.Text;
            ConnectButton.IsEnabled = false;
            try
            {
                //  ViaString does DNS resolution and a socket connect, so keep it off the UI thread
                //  (Android forbids network on the UI thread).  Link types and worker threads were
                //  set up once at app startup (see MauiProgram) using the Morph library alone - this
                //  is a pure client, with no daemon.
                BasicDefault basic = await Task.Run(() =>
                {
                    MorphApartmentProxy apartment = MorphApartmentProxy.ViaString(BasicInterface.ServiceName, new TimeSpan(0, 10, 10), new BasicFactories(), host);
                    return new BasicDefaultProxy(apartment.DefaultServlet);
                });
                _basic = basic;
                HostEntry.IsEnabled = false;
                StatusLabel.Text = "Connected to " + host + ".";
            }
            catch (Exception x)
            {
                ConnectButton.IsEnabled = true;
                await ShowException(x);
            }
        }

        #endregion

        #region Simple

        private void OnAssignNumber(object sender, EventArgs e) => Guarded(async () =>
        {
            int number = FormNumber;
            await Task.Run(() => _basic.simple.assignNumber(number));
        });

        private void OnRetrieveNumber(object sender, EventArgs e) => Guarded(async () =>
        {
            int number = await Task.Run(() => _basic.simple.retrieveNumber());
            FormNumber = number;
        });

        private void OnGetNumber(object sender, EventArgs e) => Guarded(async () =>
        {
            int number = await Task.Run(() => _basic.simple.number);
            FormNumber = number;
        });

        private void OnSetNumber(object sender, EventArgs e) => Guarded(async () =>
        {
            int number = FormNumber;
            await Task.Run(() => _basic.simple.number = number);
        });

        private void OnAssignText(object sender, EventArgs e) => Guarded(async () =>
        {
            string text = FormText;
            await Task.Run(() => _basic.simple.assignText(text));
        });

        private void OnRetrieveText(object sender, EventArgs e) => Guarded(async () =>
        {
            string text = await Task.Run(() => _basic.simple.retrieveText());
            FormText = text;
        });

        private void OnGetText(object sender, EventArgs e) => Guarded(async () =>
        {
            string text = await Task.Run(() => _basic.simple.text);
            FormText = text;
        });

        private void OnSetText(object sender, EventArgs e) => Guarded(async () =>
        {
            string text = FormText;
            await Task.Run(() => _basic.simple.text = text);
        });

        #endregion

        #region Structs and objects

        private void OnAssignStruct(object sender, EventArgs e) => Guarded(async () =>
        {
            BasicStruct value;
            value.number = FormNumber;
            value.text = FormText;
            await Task.Run(() => _basic.structs.assignStruct(value));
        });

        private void OnRetrieveStruct(object sender, EventArgs e) => Guarded(async () =>
        {
            BasicStruct value = await Task.Run(() => _basic.structs.retrieveStruct());
            FormNumber = value.number;
            FormText = value.text;
        });

        private void OnAssignClass(object sender, EventArgs e) => Guarded(async () =>
        {
            BasicClass value = new BasicClass { number = FormNumber, text = FormText };
            await Task.Run(() => _basic.structs.assignObject(value));
        });

        private void OnRetrieveClass(object sender, EventArgs e) => Guarded(async () =>
        {
            BasicClass value = await Task.Run(() => _basic.structs.retrieveObject());
            FormNumber = value.number;
            FormText = value.text;
        });

        #endregion

        #region Arrays

        private void OnAssignArray(object sender, EventArgs e) => Guarded(async () =>
        {
            char[] chars = FormText.ToCharArray();
            await Task.Run(() => _basic.arrays.assignChars(chars));
        });

        private void OnRetrieveArray(object sender, EventArgs e) => Guarded(async () =>
        {
            char[] chars = await Task.Run(() => _basic.arrays.retrieveChars());
            FormText = new string(chars);
        });

        #endregion

        #region Exceptions  (these are expected to throw;  the dialog shows them)

        private void OnCustom(object sender, EventArgs e) => Guarded(() =>
            Task.Run(() => _basic.exceptions.custom()));

        private void OnMorph(object sender, EventArgs e) => Guarded(() =>
            Task.Run(() => _basic.exceptions.morph()));

        #endregion

        #region Helpers

        private async void Guarded(Func<Task> body)
        {
            if (_basic == null)
            {
                await DisplayAlertAsync("Morph", "Connect to a server first.", "OK");
                return;
            }
            try
            {
                await body();
            }
            catch (Exception x)
            {
                await ShowException(x);
            }
        }

        private Task ShowException(Exception x)
        {
            string message = x.GetType().Name;
            if (x is EMorph morph)
                message += "\nErrorCode: " + morph.ErrorCode;
            message += "\nMessage: " + x.Message;
            return DisplayAlertAsync(x.GetType().Name, message, "OK");
        }

        #endregion
    }
}
