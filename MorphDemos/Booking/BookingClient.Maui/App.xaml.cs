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
            MainPage mainPage = new MainPage();
            Window window = new Window(mainPage)
            {
                Title = "Booking Client (Morph demo)",
                Width = 560,
                Height = 520,
            };
            //  Tear down Morph (pure client, no daemon).  First sign off from the server (send the
            //  Morph End so the server can shut down when its last client leaves), then stop the
            //  worker threads and close connections.
            window.Destroying += (sender, args) =>
            {
                mainPage.SignOff();
                ActionHandler.SetThreadCount(0);
                Connections.CloseAll();
            };
            return window;
        }
    }
}
