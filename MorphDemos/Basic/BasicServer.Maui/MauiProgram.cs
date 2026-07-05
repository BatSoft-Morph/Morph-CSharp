using Microsoft.Maui.Controls.Hosting;
using Microsoft.Maui.Hosting;

namespace BasicServer.Maui
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            MauiAppBuilder builder = MauiApp.CreateBuilder();
            builder.UseMauiApp<App>();
            return builder.Build();
        }
    }
}
