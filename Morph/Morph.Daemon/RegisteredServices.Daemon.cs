using Morph.Base;
using Morph.Endpoint;
using Morph.Internet;
using System.Net;

namespace Morph.Daemon
{
    public class LinkTypeServiceDaemon : LinkTypeService
    {
        protected override void ActionLinkService(LinkMessage message, LinkService linkService)
        {
            RegisteredService service = RegisteredServices.FindByName(linkService.ServiceName);
            if (service == null)
                throw new EMorphDaemon("Service not registered: \"" + linkService.ServiceName + "\"");
            RegisteredRunning running = service.Running;
            //  Enforce the access permissions the service registered with
            bool isLocal = true;
            if (message.Source is Connection connection)
                isLocal = Connections.IsEndPointOnThisDevice((IPEndPoint)connection.RemoteEndPoint);
            if ((isLocal && !running.AccessLocal) || (!isLocal && !running.AccessRemote))
                throw new EMorphDaemon("Access denied to service \"" + linkService.ServiceName + "\"");
            running.HandleMessage(message);
        }

        protected override void ActionLinkApartment(LinkMessage message, LinkApartment linkApartment)
        {
            RegisteredApartments.Apartments.Find(linkApartment.ApartmentID).HandleMessage(message);
        }

        protected override void ActionLinkApartmentProxy(LinkMessage message, LinkApartmentProxy linkApartmentProxy)
        {
            RegisteredApartments.ApartmentProxies.Find(linkApartmentProxy.ApartmentProxyID).HandleMessage(message);
        }
    }

    public class RegisteredRunningDaemon : RegisteredRunning
    {
        public RegisteredRunningDaemon(RegisteredService RegisteredService)
          : base(RegisteredService)
        { }

        private static readonly LinkTypeService s_linkType = new LinkTypeService();

        public override void HandleMessage(LinkMessage Message)
        {
            s_linkType.ActionLink(Message, Message.Current);
        }
    }
}