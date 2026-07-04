using Microsoft.Maui.Controls.Hosting;
using Microsoft.Maui.Hosting;

namespace Morph.Manager
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
