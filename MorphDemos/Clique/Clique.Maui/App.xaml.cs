using Clique.Interface;
using Microsoft.Maui;
using Microsoft.Maui.Controls;
using Morph.Internet;

namespace Clique.Maui
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
                Title = "Clique (Morph demo)",
                Width = 420,
                Height = 620,
            };
            //  Tear down like the original Clique.Droid.OnDestroy:  say Bye to all friends, stop the
            //  worker threads, deregister the service and close connections (daemon-less, no MorphManager).
            window.Destroying += (sender, args) =>
            {
                CliqueObjects.Finalise();
                ActionHandler.SetThreadCount(0);
                MauiProgram.CliqueService?.Deregister();
                Connections.CloseAll();
            };
            return window;
        }
    }
}
