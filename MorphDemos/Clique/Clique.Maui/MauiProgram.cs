using Clique.Interface;
using Microsoft.Maui.Controls.Hosting;
using Microsoft.Maui.Hosting;
using Morph.Base;
using Morph.Core;
using Morph.Endpoint;
using Morph.Internet;

namespace Clique.Maui
{
    public static class MauiProgram
    {
        internal static MorphService CliqueService;

        public static MauiApp CreateMauiApp()
        {
            //  Daemon-less peer (Android/Windows client), using only the Morph library - this
            //  replicates the original Xamarin Clique.Droid setup exactly:
            //  register the link types and start worker threads once, here, at app startup.
            //  Apartment-proxy IDs come from the default local IDSeed - no daemon is involved.
            LinkTypes.Register(new LinkTypeEnd());
            LinkTypes.Register(new LinkTypeMessage());
            LinkTypes.Register(new LinkTypeData());
            LinkTypes.Register(new LinkTypeInternet());
            LinkTypes.Register(new LinkTypeService());
            LinkTypes.Register(new LinkTypeServlet());
            LinkTypes.Register(new LinkTypeMember());
            ActionHandler.SetThreadCount(2);

            //  Create the Morph.Demo.Clique service so peers can call back into this instance.
            //  - the default object peers reach first (the connector)
            CliqueConnectorImpl connector = new CliqueConnectorMaui();
            //  - the apartment factory that publishes it
            MorphApartmentFactory apartmentFactory = new MorphApartmentFactoryShared(connector, CliqueInterface.Factories);
            //  - this device's own diplomat, sharing the connector's apartment
            CliqueDiplomatImpl diplomat = new CliqueDiplomatMaui();
            diplomat.MorphApartment = connector.MorphApartment;
            CliqueObjects.Initialise(diplomat);

            //  Register the factory under the service name to make it active (listenable) - all local, no network.
            CliqueService = MorphServices.Register(CliqueInterface.ServiceName, apartmentFactory);

            MauiAppBuilder builder = MauiApp.CreateBuilder();
            builder.UseMauiApp<App>();
            return builder.Build();
        }
    }
}
