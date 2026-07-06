using Microsoft.Maui.Controls.Hosting;
using Microsoft.Maui.Hosting;
using Morph.Base;
using Morph.Core;
using Morph.Endpoint;
using Morph.Internet;

namespace BookingClient.Maui
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            //  This is a daemon-less client (Android/iOS), so it uses only the Morph library:
            //  register the link types and start worker threads here, once, at app startup.
            //  Apartment-proxy IDs come from the default local IDSeed - no daemon is involved.
            LinkTypes.Register(new LinkTypeEnd());
            LinkTypes.Register(new LinkTypeMessage());
            LinkTypes.Register(new LinkTypeData());
            LinkTypes.Register(new LinkTypeInternet());
            LinkTypes.Register(new LinkTypeService());
            LinkTypes.Register(new LinkTypeServlet());
            LinkTypes.Register(new LinkTypeMember());
            ActionHandler.SetThreadCount(2);

            MauiAppBuilder builder = MauiApp.CreateBuilder();
            builder.UseMauiApp<App>();
            return builder.Build();
        }
    }
}
