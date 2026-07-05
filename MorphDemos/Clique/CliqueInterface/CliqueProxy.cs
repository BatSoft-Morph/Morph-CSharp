using Morph.Endpoint;

namespace Clique.Interface
{
    public class CliqueConnectorProxy : CliqueConnector
    {
        public CliqueConnectorProxy(ServletProxy Proxy)
          : base()
        {
            _Proxy = Proxy;
        }

        private ServletProxy _Proxy;

        public CliqueDiplomat hello(CliqueDiplomat diplomat)
        {
            return (CliqueDiplomat)_Proxy.CallMethod("hello", new object[] { diplomat }, true);
        }
    }

    public class CliqueDiplomatProxy : CliqueDiplomat
    {
        public CliqueDiplomatProxy(ServletProxy Proxy)
          : base()
        {
            _Proxy = Proxy;
        }

        private ServletProxy _Proxy;

        public string text
        {
            get { return (string)_Proxy.CallGetProperty("text", null); }
        }

        public void changeText(CliqueDiplomat friend, string text)
        {
            _Proxy.CallMethod("changeText", new object[] { friend, text }, false);
        }

        public void bye(CliqueDiplomat friend)
        {
            _Proxy.SendMethod("bye", new object[] { friend });
        }
    }
}