using Microsoft.Maui;
using Microsoft.Maui.Controls;
using Morph.Daemon.Client;

namespace Morph.Manager
{
    public partial class App : Application
    {
        public App()
        {
            InitializeComponent();
        }

        protected override Window CreateWindow(IActivationState activationState)
        {
            Window window = new Window(new AppShell())
            {
                Title = "Morph Manager",
                Width = 960,
                Height = 480,
            };
            //  The set-up connected to the daemon;  the tear-down disconnects from it
            window.Destroying += (sender, args) => MorphManager.Shutdown();
            return window;
        }
    }
}
