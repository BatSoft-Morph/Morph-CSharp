using System;
using System.Threading.Tasks;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;
using Morph;
using Morph.Endpoint;
using Morph.Params;
using MorphDemoBooking;

namespace BookingClient.Maui
{
    public partial class MainPage : ContentPage, IBookingClientUI
    {
        public MainPage()
        {
            InitializeComponent();
            //  The client's callback servlet lives in a local shared apartment.  Creating it is
            //  pure-local (no network, no daemon):  the apartment ID comes from the default local
            //  IDSeed, so this is safe in the constructor and on Android.
            MorphApartment apartment = new MorphApartmentShared(new InstanceFactories());
            _bookingClient = new BookingDiplomatClientImpl(apartment, this);
        }

        private readonly IBookingDiplomatClient _bookingClient;
        private IBookingDiplomatServer _bookingServer = null;

        #region Requesting, releasing and nudging an object

        private void OnNamesChanged(object sender, TextChangedEventArgs e)
        {
            //  Only meaningful before a booking is held;  once booked, Request stays disabled.
            if (_bookingServer == null)
                RequestButton.IsEnabled = (ClientNameEntry.Text?.Length > 0) && (ObjectNameEntry.Text?.Length > 0);
        }

        private async void OnRequest(object sender, EventArgs e)
        {
            string host = HostEntry.Text;
            string clientName = ClientNameEntry.Text;
            string objectName = ObjectNameEntry.Text;
            RequestButton.IsEnabled = false;
            try
            {
                //  First request:  connect to the server and register this client.  ViaString does a
                //  socket connect, so keep it (and every proxy call) off the UI thread - Android
                //  forbids network on the UI thread.
                if (_bookingServer == null)
                {
                    try
                    {
                        _bookingServer = await Task.Run(() =>
                        {
                            MorphApartmentProxy serverSide = MorphApartmentProxy.ViaString(BookingInterface.ServiceName, new TimeSpan(0, 30, 10), new BookingFactory(), host);
                            IBookingRegistration registration = new BookingRegistrationProxy(serverSide.DefaultServlet);
                            return registration.Register(clientName, _bookingClient);
                        });
                    }
                    catch (Exception x)
                    {
                        RequestButton.IsEnabled = true;
                        await ShowException("Ensure that the Booking Server is running:", x);
                        return;
                    }
                }
                string owner = await Task.Run(() => _bookingServer.Book(objectName));
                OwnerEntry.Text = owner;
                ClientNameEntry.IsEnabled = false;
                ObjectNameEntry.IsEnabled = false;
                RequestButton.IsEnabled = false;
                ReleaseButton.IsEnabled = true;
                NudgeButton.IsEnabled = true;
            }
            catch (Exception x)
            {
                RequestButton.IsEnabled = true;
                await ShowException(null, x);
            }
        }

        private async void OnRelease(object sender, EventArgs e)
        {
            string objectName = ObjectNameEntry.Text;
            try
            {
                string owner = await Task.Run(() => _bookingServer.Unbook(objectName));
                OwnerEntry.Text = owner;
                ClientNameEntry.IsEnabled = true;
                ObjectNameEntry.IsEnabled = true;
                RequestButton.IsEnabled = true;
                ReleaseButton.IsEnabled = false;
                NudgeButton.IsEnabled = false;
            }
            catch (Exception x)
            {
                await ShowException(null, x);
            }
        }

        private async void OnNudge(object sender, EventArgs e)
        {
            string objectName = ObjectNameEntry.Text;
            try
            {
                await Task.Run(() => _bookingServer.Nudge(objectName));
            }
            catch (Exception x)
            {
                await ShowException(null, x);
            }
        }

        #endregion

        #region IBookingClientUI  (Morph invokes these on a background thread, so marshal to the UI thread)

        public void NewOwner(string objectName, string clientName)
        {
            MainThread.BeginInvokeOnMainThread(() => OwnerEntry.Text = clientName);
        }

        public void NudgedBy(string clientName)
        {
            MainThread.BeginInvokeOnMainThread(() =>
                DisplayAlertAsync("Nudging " + ClientNameEntry.Text, clientName + " wants the object", "OK"));
        }

        #endregion

        #region Helpers

        private Task ShowException(string message, Exception x)
        {
            string body = x.GetType().Name;
            if (x is EMorph morph)
                body += "\nErrorCode: " + morph.ErrorCode;
            if (message != null)
                body = message + "\n" + body;
            body += "\nMessage: " + x.Message;
            return DisplayAlertAsync(x.GetType().Name, body, "OK");
        }

        #endregion
    }
}
