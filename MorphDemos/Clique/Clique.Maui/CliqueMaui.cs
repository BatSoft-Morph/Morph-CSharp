using System;
using Clique.Interface;

namespace Clique.Maui
{
    #region Callback routing

    //  Morph invokes the connector and diplomat callbacks on background worker threads.  Rather
    //  than let those classes know about the page, they raise a single "friends changed" signal;
    //  the page subscribes to it and refreshes the CollectionView (marshalling to the UI thread
    //  itself).  This keeps the UI-thread concern in one place - the page - exactly as the recipe
    //  requires.
    public static class CliqueNotifications
    {
        public static event Action FriendsChanged;

        internal static void RaiseFriendsChanged()
        {
            FriendsChanged?.Invoke();
        }
    }

    #endregion

    #region Morph service objects (daemon-less)

    //  The service's default object:  a remote peer calls Hello() on this to introduce itself.
    public class CliqueConnectorMaui : CliqueConnectorImpl
    {
        public CliqueConnectorMaui()
          : base()
        {
        }

        protected override void DoAddFriend(ICliqueDiplomat Friend)
        {
            CliqueNotifications.RaiseFriendsChanged();
        }

        protected override void DoDelFriend(ICliqueDiplomat Friend)
        {
            CliqueNotifications.RaiseFriendsChanged();
        }
    }

    //  This device's own diplomat:  friends call ChangeText()/Bye() on it when their Text changes
    //  or they leave.  Both cases mean the friends list needs redrawing.
    public class CliqueDiplomatMaui : CliqueDiplomatImpl
    {
        public CliqueDiplomatMaui()
          : base()
        {
        }

        public override void ChangeText(ICliqueDiplomat friend, string text)
        {
            CliqueNotifications.RaiseFriendsChanged();
        }
    }

    #endregion
}
