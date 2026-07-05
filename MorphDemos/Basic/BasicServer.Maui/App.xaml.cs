using Microsoft.Maui;
using Microsoft.Maui.Controls;
using Morph.Daemon.Client;

namespace BasicServer.Maui
{
    public partial class App : Application
    {
        public App()
        {
            InitializeComponent();
        }

        protected override Window CreateWindow(IActivationState activationState)
        {
            Window window = new Window(new MainPage())
            {
                Title = "Basic Server (Morph demo)",
                Width = 520,
                Height = 380,
            };
            //  The page started the service against the daemon;  tear it down on close
            window.Destroying += (sender, args) => MorphManager.Shutdown();
            return window;
        }
    }
}
