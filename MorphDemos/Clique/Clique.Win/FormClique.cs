using System;
using System.Windows.Forms;
using Clique.Interface;
using Morph.Daemon.Client;
using Morph.Endpoint;

namespace Clique.Win
{
  public partial class FormClique : Form
  {
    public FormClique()
    {
      InitializeComponent();
      try
      {
        //  Initialise Morph communication
        MorphManager.Startup(3);
        MorphManager.ReplyTimeout = new TimeSpan(0, 20, 0);

        //  Create the Morph.Demo.Clique service
        //  - Create default object (ie. Connector)
        CliqueConnectorImpl Connector = new CliqueConnectorWin(this);
        //  - Create the apartment factory
        MorphApartmentFactory apartmentFactory = new MorphApartmentFactoryShared(Connector, CliqueInterface.Factories);
        //  - Create a diplomat for this device
        CliqueDiplomatImpl Diplomat = new CliqueDiplomatWin(this);
        Diplomat.MorphApartment = Connector.MorphApartment;
        CliqueObjects.Initialise(Diplomat);

        //  Make the apartment factory visible under the service name "Morph.Demo.Clique" to make it active
        MorphManager.Services.StartServiceShared(CliqueInterface.ServiceName, false, true, Connector, CliqueInterface.Factories);
      }
      catch
      {
        MessageBox.Show("Morph.Daemon is not accessible.  Ensure that it is running.");
        Close();
      }
    }

    private void FormClique_FormClosing(object sender, FormClosingEventArgs e)
    {
      //  Say Bye to all friends before...
      CliqueObjects.Finalise();
      //  ...shutting down Morph communication (including any services such as Morph.Demo.Clique)
      MorphManager.Shutdown();
    }

    private void buttonConnect_Click(object sender, EventArgs e)
    {
      MorphApartmentProxy ApartmentProxy = MorphApartmentProxy.ViaString(CliqueInterface.ServiceName, new TimeSpan(0, 0, 10), CliqueInterface.Factories, textIP.Text);
      ICliqueConnector RemoteConnector = new CliqueConnectorProxy(ApartmentProxy.DefaultServlet);
      CliqueObjects.AddFriend(RemoteConnector.Hello(CliqueObjects.MyDiplomat));
    }

    private void textText_TextChanged(object sender, EventArgs e)
    {
      CliqueObjects.ChangeText(textText.Text);
    }

    public void AddFriend(ICliqueDiplomat friend)
    {
      ListViewItem Item = new ListViewItem();
      Item.Text = friend.Text;
      Item.Tag = friend;
      listFriends.Items.Add(Item);
    }

    public void DelFriend(ICliqueDiplomat friend)
    {
      for (int i = 0; i < listFriends.Items.Count; i++)
        if (listFriends.Items[i].Tag == friend)
          listFriends.Items.RemoveAt(i);
    }

    public void ChangeText(ICliqueDiplomat friend, string text)
    {
      for (int i = 0; i < listFriends.Items.Count; i++)
        if (listFriends.Items[i].Tag == friend)
          listFriends.Items[i].Text = text;
    }
  }
}