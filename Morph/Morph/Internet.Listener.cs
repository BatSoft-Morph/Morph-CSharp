using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using Morph.Lib;

namespace Morph.Internet
{
    public class Listener
    {
        internal Listener(IPEndPoint endPoint)
        {
            _endPoint = endPoint;
        }

        private readonly IPEndPoint _endPoint;
        public IPEndPoint EndPoint
        {
            get => _endPoint;
        }

        private Socket _listener;

        private Socket CreateSocket(IPEndPoint endPoint)
        {
            Socket socket = new Socket(endPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            socket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.NoDelay, 1);
            socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, 1);
            socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, 1);
            socket.Blocking = true;
            return socket;
        }

        private void AsynchListen()
        {
            Thread.CurrentThread.Name = "Listener";
            //  Work against a local reference so that Stop() nulling the field cannot cause a race
            Socket listener = _listener;
            try
            {
                try
                {
                    listener.Bind(_endPoint);
                    listener.Listen(5);
                    while (_isStarted)
                    {
                        Socket socket = listener.Accept();
                        try
                        {
                            Connections.Add(socket);
                        }
                        catch (Exception x)
                        {
                            //  One faulty incoming connection must not bring down the listener
                            MorphErrors.NotifyAbout(this, x);
                            try
                            {
                                socket.Close();
                            }
                            catch
                            {
                            }
                        }
                    }
                }
                finally
                {
                    lock (this)
                        _isActive = false;
                }
            }
            catch (Exception x)
            {
                if (_isStarted)
                    MorphErrors.NotifyAbout(this, x);
            }
        }

        private bool _isStarted = false;
        private bool _isActive = false;
        public bool IsActive
        {
            get => _isActive;
        }

        public void Start()
        {
            lock (this)
                if (!_isActive)
                {
                    //  Create the listener
                    _listener = CreateSocket(_endPoint);
                    //  Start the thread
                    try
                    {
                        _isStarted = true;
                        _isActive = true;
                        (new Thread(new ThreadStart(AsynchListen))).Start();
                    }
                    //  Just in case
                    catch
                    {
                        _isStarted = false;
                        _isActive = false;
                        _listener.Close();
                        _listener = null;
                        throw;
                    }
                }
                else if (!_isStarted)
                    throw new EMorphUsage("Cannot start while still busy stopping");
        }

        public void Stop()
        {
            lock (this)
                if (_isActive)
                {
                    //  Tell the thread to stop
                    _isStarted = false;
                    //  Close the listener
                    _listener.Close();
                    _listener = null;
                }
        }
    }

    public class Listeners
    {
        internal Listeners(List<Listener> items)
        {
            _items = items;
        }

        private readonly List<Listener> _items;

        public int Count
        {
            get => _items.Count;
        }

        public Listener this[int index]
        {
            get => _items[index];
        }

        public Listener[] ToArray()
        {
            return _items.ToArray();
        }

        public void StartAll()
        {
            foreach (Listener Listener in _items)
                Listener.Start();
        }

        public void StopAll()
        {
            foreach (Listener Listener in _items)
                Listener.Stop();
        }
    }

    public class ListenerManager
    {
        private static readonly List<Listener> s_all = new List<Listener>();

        static private bool IsLocal(IPAddress address)
        {
            IPAddress[] localAddresses = GetAllLocalAddresses();
            foreach (IPAddress LocalAddress in localAddresses)
                if (address.Equals(LocalAddress))
                    return true;
            return IPAddress.IsLoopback(address);
        }

        static public IPAddress[] GetAllLocalAddresses()
        {
            return Dns.GetHostEntry(Dns.GetHostName()).AddressList;
        }

        static public Listener Obtain(IPAddress address, int port)
        {
            return Obtain(new IPEndPoint(address, port));
        }

        static public Listener Obtain(IPEndPoint endPoint)
        {
            if (!IsLocal(endPoint.Address))
                throw new EMorphUsage("Not a local IP address");
            lock (s_all)
            {
                //  See if it already exists
                Listener portListener = Find(endPoint);
                if (portListener == null)
                { //  Create and register the listener
                    portListener = new Listener(endPoint);
                    s_all.Add(portListener);
                }
                return portListener;
            }
        }

        static public Listeners Obtain(int port)
        {
            List<Listener> items = new List<Listener>();
            //  Add all network addresses to result
            IPAddress[] addresses = GetAllLocalAddresses();
            for (int i = 0; i < addresses.Length; i++)
                items.Add(Obtain(new IPEndPoint(addresses[i], port)));
            //  Ensure loopback is included
            bool addLoopback = true;
            for (int i = 0; i < items.Count; i++)
                if (items[i].EndPoint.Address.Equals(IPAddress.Loopback))
                {
                    addLoopback = false;
                    break;
                }
            if (addLoopback)
                items.Add(Obtain(new IPEndPoint(IPAddress.Loopback, port)));
            //  Return a list of listeners
            return new Listeners(items);
        }

        static public Listener Find(IPEndPoint endPoint)
        {
            lock (s_all)
                foreach (Listener listener in s_all)
                    if (listener.EndPoint.Equals(endPoint))
                        return listener;
            return null;
        }

        static public Listeners Find(IPAddress address)
        {
            List<Listener> items = new List<Listener>();
            lock (s_all)
                foreach (Listener listener in s_all)
                    if (listener.EndPoint.Address.Equals(address))
                        items.Add(listener);
            return new Listeners(items);
        }

        static public Listeners Find(int port)
        {
            List<Listener> items = new List<Listener>();
            //  Add all network addresses to result
            lock (s_all)
                foreach (Listener listener in s_all)
                    if (listener.EndPoint.Port == port)
                        items.Add(listener);
            //  Return a list of listeners
            return new Listeners(items);
        }

        static public Listeners FindAll()
        {
            lock (s_all)
                return new Listeners(new List<Listener>(s_all));
        }

        static public void StartAll()
        {
            foreach (Listener listener in FindAll().ToArray())
                listener.Start();
        }

        static public void StopAll()
        {
            foreach (Listener listener in FindAll().ToArray())
                listener.Stop();
        }

        static public void RemoveAllInactive()
        {
            lock (s_all)
                for (int i = s_all.Count - 1; 0 <= i; i--)
                {
                    Listener listener = s_all[i];
                    if (!listener.IsActive)
                        s_all.Remove(listener);
                }
        }
    }
}