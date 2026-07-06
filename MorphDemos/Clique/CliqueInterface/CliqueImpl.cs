using System.Collections.Generic;
using Morph.Params;

namespace Clique.Interface
{
    public static class CliqueObjects
    {
        static public void Initialise(CliqueDiplomatImpl MyDiplomat)
        {
            _MyDiplomat = MyDiplomat;
        }

        static public void Finalise()
        {
            //  Bye to all
            List<ICliqueDiplomat> friends = CliqueObjects._friends;
            for (int i = 0; i < friends.Count; i++)
                friends[i].Bye(_MyDiplomat);
        }

        static internal CliqueDiplomatImpl _MyDiplomat;
        static public ICliqueDiplomat MyDiplomat
        {
            get { return _MyDiplomat; }
        }

        static private List<ICliqueDiplomat> _friends = new List<ICliqueDiplomat>();
        static public List<ICliqueDiplomat> Friends
        {
            get
            { //  Returning a copy of the list, so that UI calls can take as long as they like
                lock (_friends)
                    return new List<ICliqueDiplomat>(_friends);
            }
        }

        static public void AddFriend(ICliqueDiplomat NewFriend)
        {
            lock (_friends)
                if (!_friends.Contains(NewFriend))
                    _friends.Add(NewFriend);
        }

        static public void DelFriend(ICliqueDiplomat NewFriend)
        {
            lock (_friends)
                _friends.Remove(NewFriend);
        }

        static public void ChangeText(string text)
        {
            _MyDiplomat._text = text;
            //  Tell friends about my new Text
            List<ICliqueDiplomat> friends = Friends;
            for (int i = 0; i < friends.Count; i++)
                friends[i].ChangeText(_MyDiplomat, _MyDiplomat.Text);
        }
    }

    public abstract class CliqueConnectorImpl : MorphReference, ICliqueConnector
    {
        public CliqueConnectorImpl()
          : base(CliqueInterface.ConnectorTypeName)
        {
        }

        protected abstract void DoAddFriend(ICliqueDiplomat Friend);
        protected abstract void DoDelFriend(ICliqueDiplomat Friend);

        #region CliqueConnector interface

        public ICliqueDiplomat Hello(ICliqueDiplomat newFriend)
        {
            CliqueObjects.AddFriend(newFriend);
            DoAddFriend(newFriend);
            return CliqueObjects._MyDiplomat;
        }

        #endregion
    }

    public abstract class CliqueDiplomatImpl : MorphReference, ICliqueDiplomat
    {
        public CliqueDiplomatImpl()
          : base(CliqueInterface.DiplomatTypeName)
        {
        }

        #region CliqueDiplomat interface

        internal string _text = "";
        public string Text
        {
            get { return _text; }
        }

        public abstract void ChangeText(ICliqueDiplomat friend, string text);

        public void Bye(ICliqueDiplomat friend)
        {
            CliqueObjects.DelFriend(friend);
        }

        #endregion
    }
}