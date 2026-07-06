using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Clique.Interface;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;
using Morph.Endpoint;

namespace Clique.Maui
{
    public partial class MainPage : ContentPage
    {
        private readonly ObservableCollection<string> _friendTexts = new ObservableCollection<string>();

        public MainPage()
        {
            InitializeComponent();
            FriendsView.ItemsSource = _friendTexts;
            //  A background thread signals when the friends list changes (a peer said Hello, changed
            //  its Text, or left);  we redraw from the shared CliqueObjects.Friends each time.
            CliqueNotifications.FriendsChanged += OnFriendsChanged;
        }

        #region Connect

        private async void OnConnect(object sender, EventArgs e)
        {
            //  UI reads happen on the UI thread, before the await.
            string host = HostEntry.Text;
            ConnectButton.IsEnabled = false;
            try
            {
                //  ViaString does DNS resolution + a socket connect, and Hello() is a remote call, so
                //  the whole exchange runs off the UI thread (Android forbids network on the UI thread).
                //  This mirrors Clique.Droid.butConnectClick.
                await Task.Run(() =>
                {
                    MorphApartmentProxy apartment = MorphApartmentProxy.ViaString(CliqueInterface.ServiceName, new TimeSpan(0, 20, 10), CliqueInterface.Factories, host);
                    ICliqueConnector remoteConnector = new CliqueConnectorProxy(apartment.DefaultServlet);
                    ICliqueDiplomat friend = remoteConnector.Hello(CliqueObjects.MyDiplomat);
                    CliqueObjects.AddFriend(friend);
                });
                await RefreshFriendsAsync();
            }
            catch (Exception x)
            {
                await ShowException(x);
            }
            finally
            {
                ConnectButton.IsEnabled = true;
            }
        }

        #endregion

        #region My text

        private void OnTextChanged(object sender, TextChangedEventArgs e)
        {
            //  Read the new Text on the UI thread, then broadcast it to friends off the UI thread:
            //  ChangeText calls ChangeText() on each friend proxy, which is a network call.
            string text = TextEntry.Text ?? "";
            Task.Run(() =>
            {
                try
                {
                    CliqueObjects.ChangeText(text);
                }
                catch
                {
                    //  A friend that has gone away can throw here;  it will be pruned on its own Bye().
                }
            });
        }

        #endregion

        #region Friends list

        private void OnFriendsChanged()
        {
            //  Fire-and-forget:  read the (remote) friend texts and repaint the list.
            _ = RefreshFriendsAsync();
        }

        private async Task RefreshFriendsAsync()
        {
            //  Each friend.Text is a proxy property getter (a network call), so read them all off the
            //  UI thread;  then push the finished list of strings onto the UI thread to update the view.
            List<string> texts = await Task.Run(() =>
            {
                List<ICliqueDiplomat> friends = CliqueObjects.Friends;
                List<string> result = new List<string>(friends.Count);
                for (int i = 0; i < friends.Count; i++)
                    try
                    {
                        result.Add(friends[i].Text);
                    }
                    catch
                    {
                        result.Add("(unavailable)");
                    }
                return result;
            });

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                _friendTexts.Clear();
                for (int i = 0; i < texts.Count; i++)
                    _friendTexts.Add(texts[i]);
            });
        }

        #endregion

        #region Helpers

        private Task ShowException(Exception x)
        {
            return DisplayAlertAsync(x.GetType().Name, x.Message, "OK");
        }

        #endregion
    }
}
