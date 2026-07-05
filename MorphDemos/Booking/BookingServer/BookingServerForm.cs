using System;
using System.Windows.Forms;
using Morph.Daemon.Client;
using Morph.Sequencing;
using MorphDemoBooking;
using Bat.Library.Logging;

namespace MorphDemoBookingServer
{
  public partial class BookingServerForm : Form
  {
    public BookingServerForm()
    {
      InitializeComponent();
      Instance = this;
      ObjectInstance.OnClientIDChanged += OwnershipChanged;
      MorphManager.Startup(5);
      MorphManager.Services.StartServiceSessioned(
        BookingInterface.ServiceName,
        true, true,
        new BookingRegistrationApartmentFactory(new BookingRegistrationFactory(), new BookingInstanceFactories(), new TimeSpan(2, 0, 0), SequenceLevel.None)
        );
    }

    static public BookingServerForm Instance;

    private void BookingServerForm_FormClosed(object sender, FormClosedEventArgs e)
    {
      try
      {
        MorphManager.Services.StopService(BookingInterface.ServiceName);
        MorphManager.Shutdown();
      }
      catch (Exception x)
      {
        MessageBox.Show(x.Message, x.GetType().Name);
      }
    }

    private void OwnershipChanged(object sender, ClientIDArgs e)
    {
      try
      {
        Invoke(new ClientIDHandler(OwnershipChangedSynched), new object[] { sender, e });
      }
      catch (InvalidOperationException x)
      {
        Log.Default.Add("Outer: ", x);
        if (x.InnerException != null)
          Log.Default.Add("Inner: ", x);
      }
    }

    private void OwnershipChangedSynched(object sender, ClientIDArgs e)
    {
      ObjectInstance obj = (ObjectInstance)sender;
      //  Obtain node representing object ObjectName
      TreeNode NodeInstance;
      TreeNode[] nodes = treeBooking.Nodes.Find(obj.ObjectName, false);
      if ((nodes != null) && (nodes.Length > 0))
        NodeInstance = nodes[0];
      else
        NodeInstance = treeBooking.Nodes.Add(obj.ObjectName, obj.ObjectName);
      //  Repopulate queue
      NodeInstance.Nodes.Clear();
      for (int i = 0; i < obj.Count; i++)
        NodeInstance.Nodes.Add(Registration.ClientID_To_ClientName(obj[i]));
      //  Show all
      treeBooking.ExpandAll();
    }
  }
}