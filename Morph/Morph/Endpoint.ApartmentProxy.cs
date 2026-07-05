/**
 * An apartment proxy is the client side of a connection.
 * 
 * Use the Via... class methods to create a new apartment proxy, thereby establishing a connection to a service.
 * 
 * Parameters:
 * - Address:  This is the internet addess where the service is hosted.  This can be a domain name or an IPv4 or IPv6 internet address.
 * - ServiceName:  The name of the service that this apartment proxy aims to represent.
 * - Timeout:  How long to wait for replies to sent messages.  Timed outs raise exceptions.
 * - InstanceFactories:  A factory that converts parameters into values.  Must not be null.
 */

using System;
using System.Collections.Generic;
using System.Net;
using Morph.Base;
using Morph.Core;
using Morph.Internet;
using Morph.Lib;
using Morph.Params;
using Morph.Sequencing;

namespace Morph.Endpoint
{
    public class MorphApartmentProxy : IRegisterItemID, IDisposable, IActionLinkData, IActionLast
    {
        #region Internal

        internal MorphApartmentProxy(Device device, string serviceName, TimeSpan timeout, InstanceFactories instanceFactories)
        {
            //  Servlet proxies
            _servletProxies = new ServletProxies(this);
            _defaultServlet = _servletProxies.Obtain(Servlet.DefaultID, null);
            //  Timeout
            _timeout = timeout;
            //  Path
            _apartmentProxyLink = new LinkApartmentProxy(_apartmentProxyID);
            _device = device;
            ApartmentLink = null;
            //  Set MorphService link
            _serviceName = serviceName;
            //  Registration
            lock (s_all)
                s_all.Add(this);
            //  Instance _actories
            _instanceFactories = instanceFactories;
        }

        internal MorphApartmentProxy(Device device, int apartmentID, TimeSpan timeout, InstanceFactories instanceFactories)
        {
            //  Servlet proxies
            _servletProxies = new ServletProxies(this);
            _defaultServlet = _servletProxies.Obtain(Servlet.DefaultID, null);
            //  Timeout
            _timeout = timeout;
            //  Path
            _apartmentProxyLink = new LinkApartmentProxy(_apartmentProxyID);
            _device = device;
            ApartmentLink = new LinkApartment(apartmentID);
            //  Set MorphService link
            _serviceName = null;
            //  Registration
            lock (s_all)
                s_all.Add(this);
            //  Instance _actories
            _instanceFactories = instanceFactories;
        }

        static private bool ReadByteFromAddress(StringParser parser, out byte Byte)
        {
            Byte = 0;
            string str = parser.ReadDigits();
            if ((str == null) || (str.Length > 3))
                return false;
            uint num = uint.Parse(str);
            if (256 <= num)
                return false;
            Byte = (byte)num;
            return true;
        }

        static private IPAddress ResolveIPv4(string str)
        {
            StringParser parser = new StringParser(str);
            byte[] address = new byte[4];
            if (ReadByteFromAddress(parser, out address[0]))
                if (parser.ReadChar('.'))
                    if (ReadByteFromAddress(parser, out address[1]))
                        if (parser.ReadChar('.'))
                            if (ReadByteFromAddress(parser, out address[2]))
                                if (parser.ReadChar('.'))
                                    if (ReadByteFromAddress(parser, out address[3]))
                                        if (parser.IsEnded())
                                            return new IPAddress(address);
            return null;
        }

        static private IPAddress Resolve(string str)
        {
            IPAddress address = ResolveIPv4(str);
            if (address != null)
                return address;
            return Dns.GetHostEntry(str).AddressList[0];
        }

        private void EstablishConnection()
        {
            Call(new LinkMessage(new LinkStack(), new LinkStack(), true), out _, false);
        }

        private void EndConnection()
        {
            LinkMessage message = new LinkMessage(new LinkStack(), new LinkStack(), false);
            message.PathTo.Push(LinkTypeEnd.End);
            Send(message);
        }

        internal void Send(LinkMessage message)
        {
            //  MorphService/MorphApartment
            PushToPathTo(message);
            //  Send the message off. (No need to action the MorphApartment Proxy link.)
            message.NextLinkAction();
        }

        internal object Call(LinkMessage message, out object[] outParams, bool hasReturnValue)
        {
            //  Call number
            int callNumber = _callNumberSeed.Generate();
            message.CallNumber = callNumber;
            //  Sequence
            if (_sequenceSender != null)
                _sequenceSender.AddNextLink(false, message);
            //  MorphService/MorphApartment path
            PushToPathTo(message);
            //  Prepare to wait for the reply
            //  If the server end of this call is in this process, then this thread will end up replying to itself.
            //  Calling Prepare() here allows the server end to unblock the wait before Wait() is called.
            _waits.Prepare(callNumber);
            try
            {
                //  Send the message off. (No need to action the MorphApartment Proxy link.)
                message.NextLinkAction();
                message = null;
                //  Wait for a response
                if (_waits.Wait(callNumber, Timeout))
                    //  Received a reply
                    return _replies.GetReply(callNumber, out outParams, hasReturnValue);
                else
                    //  No reply
                    throw new EMorph("Timeout");
            }
            catch (Exception x)
            { //  Tidy up in case of error
                _replies.Remove(callNumber);
                _waits.End(callNumber);
                throw x;
            }
        }

        #endregion

        #region Constructors

        static public MorphApartmentProxy ViaLocal(string serviceName, TimeSpan timeout, InstanceFactories instanceFactories)
        {
            return ViaEndPoint(serviceName, timeout, instanceFactories, new IPEndPoint(IPAddress.Loopback, LinkInternet.MorphPort));
        }

        static public MorphApartmentProxy ViaString(string serviceName, TimeSpan timeout, InstanceFactories instanceFactories, string address)
        {
            return ViaAddress(serviceName, timeout, instanceFactories, Resolve(address));
        }

        static public MorphApartmentProxy ViaString(string serviceName, TimeSpan timeout, InstanceFactories instanceFactories, string address, int port)
        {
            return ViaEndPoint(serviceName, timeout, instanceFactories, new IPEndPoint(Resolve(address), port));
        }

        static public MorphApartmentProxy ViaAddress(string serviceName, TimeSpan timeout, InstanceFactories instanceFactories, IPAddress address)
        {
            return ViaEndPoint(serviceName, timeout, instanceFactories, new IPEndPoint(address, LinkInternet.MorphPort));
        }

        static public MorphApartmentProxy ViaEndPoint(string serviceName, TimeSpan timeout, InstanceFactories instanceFactories, IPEndPoint endPoint)
        {
            //  Add Internet link
            LinkStack devicePath = new LinkStack();
            if (endPoint != null)
                devicePath.Push(LinkInternet.New(endPoint));
            //  Obtain the device
            Device device = Devices.Obtain(devicePath);
            //  Create MorphApartment Proxy
            MorphApartmentProxy newProxy = new MorphApartmentProxy(device, serviceName, timeout, instanceFactories);
            //  Establish connection
            newProxy.EstablishConnection();
            //  Return the proxy object (removing redundancy).
            //  The redundant proxy shares oldProxy's remote apartment, so dispose it locally only.
            MorphApartmentProxy oldProxy = device.Find(newProxy.ApartmentID);
            if (oldProxy != newProxy)
                newProxy.Dispose(false);
            return oldProxy;
        }

        #endregion

        #region IDisposable Members

        private bool _disposed = false;

        public void Dispose()
        {
            Dispose(true);
        }

        //  notifyRemote:  when false, only local registrations are released and the remote apartment
        //  is left alone (used when discarding a redundant proxy that shares another proxy's apartment).
        private void Dispose(bool notifyRemote)
        {
            lock (s_all)
            {
                if (_disposed)
                    return;
                _disposed = true;
                s_all.Remove(this);
            }
            if ((_device != null) && (ApartmentID != 0))
                lock (_device._apartmentProxiesByApartmentID)
                    if (_device._apartmentProxiesByApartmentID[ApartmentID] == this)
                        _device._apartmentProxiesByApartmentID.Remove(ApartmentID);
            if (_sequenceSender != null)
            {
                _sequenceSender.Halt();
                _sequenceSender = null;
            }
            if (notifyRemote)
                EndConnection();
            //  Disposed properly, so the finalizer need not run
            GC.SuppressFinalize(this);
        }

        #endregion

        #region Register

        ~MorphApartmentProxy()
        {
            //  A finalizer must not do network I/O nor let an exception escape;  release local state only
            try
            {
                Dispose(false);
            }
            catch
            {
            }
        }

        #region RegisterItemID Members

        internal int ApartmentID
        {
            get
            {
                if (ApartmentLink == null)
                    return 0;
                return ApartmentLink.ApartmentID;
            }
        }

        private readonly int _apartmentProxyID = IDFactory.Generate();
        public int ID
        {
            get => _apartmentProxyID;
        }

        static public IIDFactory IDFactory = new IDSeed();

        #endregion

        private static readonly RegisterItems<MorphApartmentProxy> s_all = new RegisterItems<MorphApartmentProxy>();

        static public MorphApartmentProxy Find(int id)
        {
            lock (s_all)
                return s_all.Find(id);
        }

        #endregion

        #region ServletProxies

        private readonly ServletProxies _servletProxies;
        internal ServletProxies ServletProxies
        {
            get => _servletProxies;
        }

        public ServletProxy FindServlet(int ServletID)
        {
            return _servletProxies.Find(ServletID);
        }

        private readonly ServletProxy _defaultServlet;
        public ServletProxy DefaultServlet
        {
            get => _defaultServlet;
        }

        #endregion

        #region Timeout

        private TimeSpan _timeout;
        public TimeSpan Timeout
        {
            get => _timeout;
        }

        #endregion

        #region Path

        private readonly LinkApartmentProxy _apartmentProxyLink;

        private readonly Device _device;
        public Device Device
        {
            get => _device;
        }

        private readonly string _serviceName;

        private LinkApartment _apartmentLink = null;
        internal LinkApartment ApartmentLink
        {
            get => _apartmentLink;
            set
            {
                if (ApartmentID == (value == null ? 0 : value.ApartmentID))
                    return;
                if ((_device != null) && (_apartmentLink != null))
                    lock (_device._apartmentProxiesByApartmentID)
                        _device._apartmentProxiesByApartmentID.Remove(ApartmentID);
                _apartmentLink = value;
                if ((_device != null) && (_apartmentLink != null))
                    lock (_device._apartmentProxiesByApartmentID)
                        if (_device._apartmentProxiesByApartmentID[ApartmentID] == null)
                            _device._apartmentProxiesByApartmentID.Add(ApartmentID, this);
            }
        }

        public bool RequiresFromPath
        {
            get => true;
        }

        public LinkStack Path
        {
            get
            {
                LinkStack fullPath = new LinkStack();
                if (ApartmentLink != null)
                    fullPath.Push(ApartmentLink);
                else if (_serviceName != null)
                    fullPath.Push(new LinkService(_serviceName));
                fullPath.Push(_device.Path);
                fullPath.Push(_apartmentProxyLink);
                return fullPath;
            }
        }

        internal void SetPath(LinkStack path)
        {
            if (path == null)
                throw new EMorphUsage("A proper path to the apartment is expected");
            List<Link> links = path.ToLinks();
            //  Find this apartment proxy
            Link link = null;
            if (links.Count > 0)
            {
                link = links[links.Count - 1];
                links.RemoveAt(links.Count - 1);
            }
            if ((link == null) || !(link is LinkApartmentProxy) || (((LinkApartmentProxy)link).ApartmentProxyID != _apartmentProxyID))
                throw new EMorphImplementation();
            //  Find the apartment link
            for (int i = links.Count - 1; i >= 0; i--)
            {
                link = links[0];
                links.RemoveAt(0);
                if (link is LinkApartment)
                {
                    ApartmentLink = (LinkApartment)link;
                    _device._path = new LinkStack(links);
                    return;
                }
            }
            throw new EMorph("Path did not include apartment link");
        }

        public void PushToPathTo(LinkMessage message)
        {
            if (ApartmentLink != null)
                message.PathTo.Push(ApartmentLink);
            else if (_serviceName != null)
                message.PathTo.Push(new LinkService(_serviceName));
            message.PathTo.Push(_device.Path);
            message.PathTo.Push(_apartmentProxyLink);
        }

        #endregion

        #region Reply handling

        private readonly IDSeed _callNumberSeed = new IDSeed();

        private readonly Replies _replies = new Replies();

        private readonly NumberedWaits _waits = new NumberedWaits();

        private void HandleReplyBegin(LinkMessage Message)
        {
            /*  Sequence
            if (NextLink is LinkSequence)
            {
              Message.ActionNext();
              MorphApartmentProxy._SequenceSender = (SequenceSender)((LinkSequence)NextLink).FindLinkObject();
            }
             */
            //  Find call number to match response with waiting thread
            int callNumber = Message.CallNumber;
            //  Tell the calling thread to hold on, to prevent a timeout while we're assigning the return data.
            //  If Hold() fails, then it's because no thread is waiting for this reply.
            if (!_waits.Hold(callNumber))
                throw new EMorph("No one waiting for reply");
            //  Update the proxy's path to the apartment
            if (Message.HasPathFrom)
                SetPath(Message.PathFrom);
        }

        private void HandleReplyEnd(int callNumber)
        {
            _waits.End(callNumber);
        }

        #endregion

        #region Instance factories

        private InstanceFactories _instanceFactories;
        public InstanceFactories InstanceFactories
        {
            get => _instanceFactories;
            set => _instanceFactories = value;
        }

        #endregion

        #region Sequencing

        internal SequenceSender _sequenceSender = null;
        public SequenceLevel SequenceLevel
        {
            get
            {
                if (_sequenceSender == null)
                    return SequenceLevel.None;
                if (_sequenceSender.IsLossless)
                    return SequenceLevel.Lossless;
                else
                    return SequenceLevel.Lossy;
            }
        }

        #endregion

        #region IActionLinkData

        public void ActionLinkData(LinkMessage message, LinkData data)
        {
            int callNumber = message.CallNumber;
            try
            {
                HandleReplyBegin(message);
                _replies.AssignReply(callNumber, InstanceFactories, Device, message.PathFrom, data);
            }
            finally
            {
                HandleReplyEnd(callNumber);
            }
        }

        #endregion

        #region IActionLast

        public void ActionLast(LinkMessage message)
        {
            int callNumber = message.CallNumber;
            try
            {
                HandleReplyBegin(message);
                _replies.AssignReply(callNumber, InstanceFactories, Device, null, null);
            }
            finally
            {
                HandleReplyEnd(callNumber);
            }
        }

        #endregion
    }
}