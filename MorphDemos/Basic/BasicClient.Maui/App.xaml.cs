using Microsoft.Maui;
using Microsoft.Maui.Controls;
using Morph.Internet;

namespace BasicClient.Maui
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
                Title = "Basic Client (Morph demo)",
                Width = 560,
                Height = 640,
            };
            //  Tear down Morph (pure client, no daemon):  stop the worker threads and close connections.
            window.Destroying += (sender, args) =>
            {
                ActionHandler.SetThreadCount(0);
                Connections.CloseAll();
            };
            return window;
        }
    }
}
