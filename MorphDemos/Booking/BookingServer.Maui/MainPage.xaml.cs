using System;
using System.Collections.ObjectModel;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;
using Morph.Daemon.Client;
using Morph.Sequencing;
using MorphDemoBooking;
using MorphDemoBookingServer;

namespace BookingServer.Maui
{
    public partial class MainPage : ContentPage
    {
        public MainPage()
        {
            InitializeComponent();
            BookingTree.ItemsSource = _objects;
        }

        private bool _started = false;

        #region Registration tree  (grouped:  each booked object holds the queue of client names)

        private readonly ObservableCollection<ObjectGroup> _objects = new ObservableCollection<ObjectGroup>();

        private ObjectGroup GroupFor(string objectName)
        {
            foreach (ObjectGroup group in _objects)
                if (group.ObjectName == objectName)
                    return group;
            ObjectGroup created = new ObjectGroup(objectName);
            _objects.Add(created);
            return created;
        }

        #endregion

        #region Service lifetime

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            if (_started)
                return;
            _started = true;
            try
            {
                //  Morph raises OwnershipChanged on a background thread;  marshal to the UI thread.
                ObjectInstance.OnClientIDChanged += OwnershipChanged;
                MorphManager.Startup(5);
                MorphManager.ReplyTimeout = new TimeSpan(0, 20, 0);
                MorphManager.Services.StartServiceSessioned(
                    BookingInterface.ServiceName,
                    true, true,
                    new BookingRegistrationApartmentFactory(new BookingRegistrationFactory(), new BookingInstanceFactories(), new TimeSpan(2, 0, 0), SequenceLevel.None));
                StatusLabel.Text = "Service '" + BookingInterface.ServiceName + "' is running.  Waiting for a client.";
            }
            catch (Exception x)
            {
                StatusLabel.Text = "Could not start the service.";
                await DisplayAlertAsync("Morph", "Could not reach the Morph daemon.  Ensure Morph.Daemon is running.\n\n" + x.Message, "OK");
            }
        }

        #endregion

        #region Ownership changes  (Morph invokes this on a background thread, so marshal to the UI thread)

        private void OwnershipChanged(object sender, ClientIDArgs e)
        {
            ObjectInstance obj = (ObjectInstance)sender;
            OnUI(() => Repopulate(obj));
        }

        //  Rebuild one object's group from the live queue  (mirrors OwnershipChangedSynched).
        private void Repopulate(ObjectInstance obj)
        {
            ObjectGroup group = GroupFor(obj.ObjectName);
            group.Clear();
            for (int i = 0; i < obj.Count; i++)
                group.Add(Registration.ClientID_To_ClientName(obj[i]));
        }

        private static void OnUI(Action action)
        {
            if (MainThread.IsMainThread)
                action();
            else
                MainThread.InvokeOnMainThreadAsync(action).GetAwaiter().GetResult();
        }

        #endregion
    }

    //  One booked object and the ordered queue of client names waiting for it.
    public class ObjectGroup : ObservableCollection<string>
    {
        public ObjectGroup(string objectName)
        {
            ObjectName = objectName;
        }

        public string ObjectName { get; }
    }
}
