using System;
using System.Collections.Generic;
using System.Reflection;
using Morph.Base;
using Morph.Core;

namespace Morph.Endpoint
{
    public abstract class LinkMember : Link, IActionLinkData, IActionLast
    {
        public LinkMember()
          : base(LinkTypeID.Member)
        {
        }

        public abstract string Name
        {
            get;
        }

        private LinkStack DevicePathOf(LinkStack path)
        {
            if (path == null)
                return null;
            List<Link> links = path.ToLinks();
            for (int i = links.Count - 1; i >= 0; i--)
            {
                Link link = links[i];
                if ((link is LinkApartment) ||
                    (link is LinkApartmentProxy) ||
                    (link is LinkService) ||
                    (link is LinkServlet) ||
                    (link is LinkMember) ||
                    (link is LinkData))
                    links.RemoveAt(i);
            }
            return new LinkStack(links);
        }

        private LinkStack EndpointPathOf(LinkStack path)
        {
            if (path == null)
                return null;
            List<Link> links = path.ToLinks();
            for (int i = links.Count - 1; i >= 0; i--)
            {
                Link link = links[i];
                if ((link is LinkServlet) ||
                    (link is LinkMember) ||
                    (link is LinkData))
                    links.RemoveAt(i);
            }
            return new LinkStack(links);
        }

        protected internal Servlet _servlet;

        protected internal abstract LinkData Invoke(LinkMessage message, LinkStack senderDevicePath, LinkData dataIn);

        #region IActionLinkData

        public void ActionLinkData(LinkMessage message, LinkData dataIn)
        {
            //  Obtain apartment
            MorphApartment apartment = _servlet.Apartment;
            try
            {
                //  Get a device path
                LinkStack pathToSender = null;
                if (apartment is MorphApartmentSession session)
                    pathToSender = session.Path;
                else if (message.HasPathFrom)
                    pathToSender = message.PathFrom;
                //  Invoke the method
                LinkData dataOut = Invoke(message, DevicePathOf(pathToSender), dataIn);
                //  Send a reply
                SendReply(message, apartment, dataOut);
            }
            catch (EMorph x)
            {
                SendReply(message, apartment, new LinkData(x));
            }
            catch (TargetInvocationException x)
            {
                Exception y = x.InnerException;
                SendReply(message, apartment, new LinkData(y));
            }
            catch (Exception x)
            {
                SendReply(message, apartment, new LinkData(x));
            }
        }

        #endregion

        #region IActionLast

        public void ActionLast(LinkMessage message)
        {
            ActionLinkData(message, null);
        }

        #endregion

        private void SendReply(LinkMessage message, MorphApartment apartment, Link payload)
        {
            //  Identify cases when we don't reply
            if (!message.HasCallNumber)
                return; //  CallNumber is required on the calling end to match call and reply
            if (!message.HasPathFrom && !(apartment is MorphApartmentSession))
                return; //  Wouldn't know where to reply to        
                        //  In this implementation, we only bother replying with a from path if the call had a from path.
            LinkStack pathFrom = null;
            if (message.HasPathFrom)
                pathFrom = new LinkStack();
            //  Build a destination (return) path
            LinkStack pathTo = null;
            if (apartment is MorphApartmentSession sessionApartment)
                pathTo = sessionApartment.GenerateReturnPath();
            else if (message.HasPathFrom)
                pathTo = EndpointPathOf(message.PathFrom);
            pathTo.Append(payload);
            //  Build reply message
            LinkMessage replyMessage = new LinkMessage(pathTo, pathFrom, message.IsForceful);
            if (message.HasCallNumber)
                replyMessage.CallNumber = message.CallNumber;
            //  Send the reply
            replyMessage.NextLinkAction();
        }

        public override bool Equals(object obj)
        {
            return (obj is LinkMember linkMember) && (linkMember.Name.ToLower().Equals(Name.ToLower()));
        }

        public override int GetHashCode()
        {
            return Name.GetHashCode();
        }
    }

    public class LinkTypeMember : ILinkTypeReader, ILinkTypeAction
    {
        public LinkTypeID ID
        {
            get => LinkTypeID.Member;
        }

        public Link ReadLink(MorphReader reader)
        {
            bool isProperty, isSet, hasIndex;
            reader.ReadLinkByte(out isProperty, out isSet, out hasIndex);
            string name = reader.ReadString();
            if (isProperty)
                return new LinkProperty(name, isSet, hasIndex);
            else
                return new LinkMethod(name);
        }

        public void ActionLink(LinkMessage message, Link currentLink)
        {
            //  Obtain servlet
            Servlet servlet;
            if (message.ContextIs(typeof(Servlet)))
                servlet = (Servlet)message.Context;
            else if (message.ContextIs(typeof(MorphApartment)))
                servlet = ((MorphApartment)message.Context).DefaultServlet;
            else
                throw new EMorph("Link type not supported by context");
            //  Hold on to the servlet
            ((LinkMember)currentLink)._servlet = servlet;
            //  Move along
            message.Context = currentLink;
            message.NextLinkAction();
        }
    }
}