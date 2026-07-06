using Morph.Endpoint;

namespace Clique.Interface
{
    public class CliqueConnectorProxy : ICliqueConnector
    {
        public CliqueConnectorProxy(ServletProxy Proxy)
          : base()
        {
            _Proxy = Proxy;
        }

        private ServletProxy _Proxy;

        public ICliqueDiplomat Hello(ICliqueDiplomat diplomat)
        {
            return (ICliqueDiplomat)_Proxy.CallMethod("hello", new object[] { diplomat }, true);
        }
    }

    public class CliqueDiplomatProxy : ICliqueDiplomat
    {
        public CliqueDiplomatProxy(ServletProxy Proxy)
          : base()
        {
            _Proxy = Proxy;
        }

        private ServletProxy _Proxy;

        public string Text
        {
            get { return (string)_Proxy.CallGetProperty("text", null); }
        }

        public void ChangeText(ICliqueDiplomat friend, string text)
        {
            _Proxy.CallMethod("changeText", new object[] { friend, text }, false);
        }

        public void Bye(ICliqueDiplomat friend)
        {
            _Proxy.SendMethod("bye", new object[] { friend });
        }
    }
}