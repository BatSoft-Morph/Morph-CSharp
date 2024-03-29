﻿using Android.App;
using Android.OS;
using Android.Widget;
using Morph.Base;
using Morph.Core;
using Morph.Endpoint;
using Morph.Internet;
using MorphDemoBooking;
using MorphDemoBookingClient;
using System;
using System.Net;

namespace MorphDemoBookingClientAndroid
{
    [Activity(Label = "MorphDemoBookingClientAndroid", MainLauncher = true, Icon = "@drawable/icon")]
    public class ActivityDemoBooking : Activity
    {
        protected override void OnCreate(Bundle bundle)
        {
            base.OnCreate(bundle);

            //  Setup UI
            SetContentView(Resource.Layout.Main);
            //  - Controls
            edIP = FindViewById<EditText>(Resource.Id.edIP);
            edClientName = FindViewById<EditText>(Resource.Id.edMyName);
            edObjectName = FindViewById<EditText>(Resource.Id.edObjectName);
            edCurrentOwner = FindViewById<EditText>(Resource.Id.edCurrentOwner);
            butRequest = FindViewById<Button>(Resource.Id.butRequest);
            butRelease = FindViewById<Button>(Resource.Id.butRelease);
            butNudge = FindViewById<Button>(Resource.Id.butNudge);
            butExit = FindViewById<Button>(Resource.Id.butExit);
            //  Control states
            //edClientName.setIsInEditMode = true;
            //edObjectName.IsInEditMode = true;
            //edCurrentOwner.IsInEditMode = false;
            //  - Events
            butRequest.Click += RequestClick;
            butRelease.Click += ReleaseClick;
            butNudge.Click += NudgeClick;
            butExit.Click += ExitClick;

            //  Register link types        
            LinkTypes.Register(new LinkTypeEnd());
            LinkTypes.Register(new LinkTypeMessage());
            LinkTypes.Register(new LinkTypeData());
            LinkTypes.Register(new LinkTypeInternet());
            LinkTypes.Register(new LinkTypeService());
            LinkTypes.Register(new LinkTypeServlet());
            LinkTypes.Register(new LinkTypeMember());
            //  Setup Morph interface
            ActionHandler.SetThreadCount(2);
            _bookingFactory = new BookingFactory();
            MorphApartment apartment = new MorphApartmentShared(_bookingFactory);
            _localDiplomat = new BookingDiplomatClientImpl(apartment, this);
            _serverDiplomat = null;
        }

        protected override void OnDestroy()
        {
            if (_serverDiplomat != null)
            {
                //  Be polite and unbook
                _serverDiplomat.Unbook(edObjectName.Text);
                //  Be polite and tell the server to release resources that have been allocated for this client
                ((BookingDiplomatServerProxy)_serverDiplomat).ServletProxy.ApartmentProxy.Dispose();
            }
            ActionHandler.SetThreadCount(0);
            Connections.CloseAll();
            base.OnDestroy();
        }

        #region UI

        private EditText edIP;
        private EditText edClientName;
        private EditText edObjectName;
        private EditText edCurrentOwner;
        private Button butRequest;
        private Button butRelease;
        private Button butNudge;
        private Button butExit;

        private void ShowMessage(string Message)
        {
            Toast.MakeText(this, Message, ToastLength.Short);
        }

        private void ShowError(Exception x)
        {
            ShowMessage(x.GetType().Name + ": " + x.Message);
        }

        private void RequestClick(object sender, EventArgs e)
        {
            IPAddress address;
            if (_serverDiplomat != null)
                ShowMessage("Already waiting for object " + edObjectName.Text + ".");
            else if (!IPAddress.TryParse(edIP.Text, out address))
                ShowMessage("Not a valid IP address:/n" + edIP.Text);
            else
                try
                {
                    MorphApartmentProxy apartmentProxy = MorphApartmentProxy.ViaAddress("Morph.Demo.Booking", new TimeSpan(0, 10, 10), _bookingFactory, address);
                    IBookingRegistration _Registration = new BookingRegistrationProxy(apartmentProxy.DefaultServlet);
                    _serverDiplomat = _Registration.Register(edClientName.Text, _localDiplomat);
                    edCurrentOwner.Text = _serverDiplomat.Book(edObjectName.Text);
                    //edClientName.setIsInEditMode = false;
                    //edObjectName.IsInEditMode = false;
                }
                catch (Exception x)
                {
                    ShowError(x);
                }
        }

        private void ReleaseClick(object sender, EventArgs e)
        {
            if (_serverDiplomat == null)
                ShowMessage("Already waiting for object " + edObjectName.Text + ".");
            else
                try
                {
                    edCurrentOwner.Text = _serverDiplomat.Unbook(edObjectName.Text);
                    _serverDiplomat = null;
                }
                catch (Exception x)
                {
                    ShowError(x);
                }
        }

        private void NudgeClick(object sender, EventArgs e)
        {
            try
            {
                _serverDiplomat.Nudge(edObjectName.Text);
            }
            catch (Exception x)
            {
                ShowError(x);
            }
        }

        private void ExitClick(object sender, EventArgs e)
        {
            Finish();
        }

        #endregion

        #region Morph interface

        private BookingFactory _bookingFactory;
        private IBookingDiplomatClient _localDiplomat;
        private IBookingDiplomatServer _serverDiplomat;

        public void NewOwner(string objectName, string clientName)
        {
            edObjectName.Text = objectName;
            edClientName.Text = clientName;
            edObjectName.PostInvalidate();
            edObjectName.PostInvalidate();
        }

        public void NudgedBy(string clientName)
        {
            Toast.MakeText(this, "Client " + clientName + " wants the object.", ToastLength.Short);
        }

        #endregion
    }
}