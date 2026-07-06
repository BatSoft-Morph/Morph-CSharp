using Microsoft.Maui;
using Microsoft.Maui.Controls;
using Morph.Internet;

namespace BookingClient.Maui
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
                Title = "Booking Client (Morph demo)",
                Width = 560,
                Height = 520,
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
