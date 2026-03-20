using System;
using System.Windows.Forms;
using Morph.Daemon.Client;
using Morph.Endpoint;
using Morph.Params;
using MorphDemoBooking;

namespace MorphDemoBookingClient
{
  public partial class BookingClientForm : Form
  {
    public BookingClientForm()
    {
      InitializeComponent();
      try
      {
        MorphManager.Startup(5);
        MorphManager.ReplyTimeout = new TimeSpan(0, 0, 20);
        MorphApartment apartment = new MorphApartmentShared(new InstanceFactories());
        _bookingClient = new BookingDiplomatClientImpl(apartment, this);
      }
      catch
      {
        MessageBox.Show("Morph.Daemon is not accessible.  Ensure that it is running.");
        Close();
      }
    }

    private void ShowException(string message, Exception x)
    {
      if (message != null)
        message = message + "\u000D\u000A" + x.Message + "\u000D\u000A" + x.StackTrace;
      else
        message = x.Message + "\u000D\u000A" + x.StackTrace;
      MessageBox.Show(message, x.GetType().Name);
    }

    private IBookingDiplomatServer _bookingServer = null;
    private IBookingDiplomatClient _bookingClient = null;

    private void butClose_Click(object sender, EventArgs e)
    {
      Close();
    }

    private void BookingClientForm_FormClosing(object sender, FormClosingEventArgs e)
    {
      if (butRelease.Enabled)
        butRelease_Click(sender, e);
      if (_bookingServer != null)
        ((BookingDiplomatServerProxy)_bookingServer).ServletProxy.ApartmentProxy.Dispose();
      MorphManager.Shutdown();
    }

    private void textClientName_TextChanged(object sender, EventArgs e)
    {
      butRequest.Enabled = (textClientName.Text.Length > 0) && (textObjectName.Text.Length > 0);
    }

    private void textObjectName_TextChanged(object sender, EventArgs e)
    {
      butRequest.Enabled = (textClientName.Text.Length > 0) && (textObjectName.Text.Length > 0);
    }

    private void butRequest_Click(object sender, EventArgs e)
    {
      try
      {
        if (_bookingServer == null)
          try
          {
            MorphApartmentProxy ServerSide = MorphApartmentProxy.ViaLocal(BookingInterface.ServiceName, new TimeSpan(0, 30, 10), new BookingFactory());
            IBookingRegistration Registration = new BookingRegistrationProxy(ServerSide.DefaultServlet);
            _bookingServer = Registration.Register(textClientName.Text, _bookingClient);
          }
          catch (Exception x)
          {
            ShowException("Ensure that the Booking Server is running:", x);
            return;
          }
        textOwner.Text = _bookingServer.Book(textObjectName.Text);
        textClientName.Enabled = false;
        textObjectName.Enabled = false;
        butRequest.Enabled = false;
        butRelease.Enabled = true;
        butNudge.Enabled = true;
      }
      catch (Exception x)
      {
        ShowException(null, x);
      }
    }

    private void butRelease_Click(object sender, EventArgs e)
    {
      try
      {
        textOwner.Text = _bookingServer.Unbook(textObjectName.Text);
        textClientName.Enabled = true;
        textObjectName.Enabled = true;
        butRequest.Enabled = true;
        butRelease.Enabled = false;
        butNudge.Enabled = false;
      }
      catch (Exception x)
      {
        ShowException(null, x);
      }
    }

    private void butNudge_Click(object sender, EventArgs e)
    {
      try
      {
        _bookingServer.Nudge(textObjectName.Text);
      }
      catch (Exception x)
      {
        ShowException(null, x);
      }
    }

    #region IBookingDiplomatClient implementation

    public void NewOwner(string objectName, string clientName)
    {
      textOwner.Text = clientName;
    }

    public void NudgedBy(string clientName)
    {
      MessageBox.Show(this, clientName + " wants the object", "Nudging " + textClientName.Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    #endregion
  }
}