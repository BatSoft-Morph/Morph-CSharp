using Clique.Interface;

namespace Clique.Win
{
    delegate void AddDelFriend(ICliqueDiplomat friend);
    delegate void ChangeText(ICliqueDiplomat friend, string text);

    public class CliqueConnectorWin : CliqueConnectorImpl
    {
        public CliqueConnectorWin(FormClique Form)
          : base()
        {
            _Form = Form;
        }

        private FormClique _Form;

        protected override void DoAddFriend(ICliqueDiplomat Friend)
        {
            _Form.Invoke(new AddDelFriend(_Form.AddFriend), new object[] { Friend });
        }

        protected override void DoDelFriend(ICliqueDiplomat Friend)
        {
            _Form.Invoke(new AddDelFriend(_Form.DelFriend), new object[] { Friend });
        }
    }

    public class CliqueDiplomatWin : CliqueDiplomatImpl
    {
        public CliqueDiplomatWin(FormClique Form)
          : base()
        {
            _Form = Form;
        }

        private FormClique _Form;

        public override void ChangeText(ICliqueDiplomat friend, string text)
        {
            _Form.Invoke(new ChangeText(_Form.ChangeText), new object[] { friend, text });
        }
    }
}