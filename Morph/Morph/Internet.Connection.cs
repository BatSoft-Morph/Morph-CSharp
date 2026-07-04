using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using Morph.Base;
using Morph.Core;
using Morph.Lib;

namespace Morph.Internet
{
    public class LinkMessageFromIP : LinkMessage
    {
        public LinkMessageFromIP(LinkStack pathTo, LinkStack pathFrom, bool isForceful, Connection connection)
          : base(pathTo, pathFrom, isForceful)
        {
            _connection = connection;
        }

        private Connection _connection;
        public Connection Connection
        {
            get => _connection;
        }
    }

    internal class DataInHandler
    {
        public DataInHandler(Connection connection)
        {
            _connection = connection;
            _stream = new MorphStream();
            _reader = new MorphReaderSizeless(_stream);
        }

        //  Incoming data
        private readonly Connection _connection;

        private readonly MorphStream _stream;
        private readonly MorphReader _reader;

        //  Message information

        private enum Stage { Clear, LinkByte, Message, Closed };

        private Stage _stage = Stage.Clear;
        private LinkTypeID _linkTypeID;
        private bool _hasCallNumber, _isForceful, _hasPathFrom;
        private int _callNumber, _pathToLength, _pathFromLength;

        private void ClearToLinkByte()
        {
            if ((_stage == Stage.Clear) && (_stream.Remaining > 0))
            {
                //  Examine in the link byte
                _linkTypeID = _reader.ReadLinkByte(out _hasCallNumber, out _isForceful, out _hasPathFrom);
                //  Next stage
                _stage = Stage.LinkByte;
            }
        }

        private void LinkTypeToMessage()
        {
            if (_stage == Stage.LinkByte)
                try
                {
                    //  If received a LinkEnd message to the socket, then end the socket
                    if (_linkTypeID == LinkTypeID.End)
                    {
                        //  Next stage
                        _stage = Stage.Closed;
                        //  Close the connection
                        _connection.Close();
                        return;
                    }
                    //  If receiving a LinkMessage, then see if there's enough data for reading it in
                    if (_linkTypeID == LinkTypeID.Message)
                    {
                        if (_stream.Remaining >= 4 + (_hasCallNumber ? 4 : 0) + (_hasPathFrom ? 4 : 0))
                        {
                            //  - CallNumber
                            _callNumber = _hasCallNumber ? _reader.ReadInt32() : 0;
                            //  - PathTo size
                            _pathToLength = _reader.ReadInt32();
                            //  - PathFrom size
                            _pathFromLength = _hasPathFrom ? _reader.ReadInt32() : 0;
                            //  Next stage
                            _stage = Stage.Message;
                        }
                    }
                    else
                        //  No other link types are accepted outside here
                        throw new EMorph("Unexpected link type");
                }
                catch
                {
                    _connection.Close();
                    throw;
                }
        }

        private void MessageToClear()
        {
            if ((_stage == Stage.Message) && (_pathToLength + _pathFromLength <= _stream.Remaining))
            {
                //  Read PathTo 
                LinkStack pathTo = new LinkStack(_reader.SubReader(_pathToLength));
                //  Read PathFrom
                LinkStack pathFrom = null;
                if (_hasPathFrom)
                    if (_pathFromLength == 0)
                        pathFrom = new LinkStack();
                    else
                        pathFrom = new LinkStack(_reader.SubReader(_pathFromLength));
                //  Create message
                LinkMessage message = new LinkMessageFromIP(pathTo, pathFrom, _isForceful, _connection);
                message.Source = _connection;
                if (_hasCallNumber) message.CallNumber = _callNumber;
                message.IsForceful = _isForceful;
                //  Apply NAT workaround for IPv4
                if ((_connection.Socket.AddressFamily == AddressFamily.InterNetwork) && (pathFrom != null))
                    message.PathFrom.Push(new LinkInternetIPv4((IPEndPoint)_connection.Socket.RemoteEndPoint));
                //  Add the (now completely received) message to the action queue
                ActionHandler.Add(message);
                //  Next stage
                _stage = Stage.Clear;
            }
        }

        public void AddData(byte[] data, int offset, int count)
        {
            if ((data == null) || (count == 0))
                return;
            //  Write data to stream
            _stream.Write(data, offset, count);
            //  Read messages from stream
            do
            {
                ClearToLinkByte();
                LinkTypeToMessage();
                MessageToClear();
            } while ((_stage == Stage.Clear) && (_stream.Remaining > 0));
        }
    }

    public class Connection : IRegisterItemName
    {
        internal Connection(Socket socket)
        {
            _socket = socket;
            _name = _socket.RemoteEndPoint.ToString();
            _dataIn = new DataInHandler(this);
            SendMorphValidation();
            Connections.Add(this);
            StartReceivingData();
        }

        #region Connection validation

        //  "Morph"#0 + Major version:2 + Minor version:1
        //  (Major version 2:  the reworked ValueType/SimpleType wire format is incompatible with version 1.)
        private static readonly byte[] s_MorphValidation = new byte[] { 0x4D, 0x6F, 0x72, 0x70, 0x68, 0x00, 0x02, 0x01 };

        private readonly ManualResetEvent _morphValidationSent = new ManualResetEvent(false);

        private void SendMorphValidation()
        {
            _socket.Send(s_MorphValidation);
            _morphValidationSent.Set();
        }

        private void TestMorphValidation()
        {
            byte[] buffer = new byte[8];
            if (buffer.Length != _socket.Receive(buffer))
                throw new EMorph("Remote connection appears to not be a Morph connection.");
            for (int i = 5; i >= 0; i--)
                if (buffer[i] != s_MorphValidation[i])
                    throw new EMorph("Remote connection appears to not be a Morph connection.");
            if (buffer[6] != s_MorphValidation[6])
                throw new EMorph("Incompatible Major versions of Morph.");
            if (buffer[7] != s_MorphValidation[7])
                throw new EMorph("Incompatible Minor versions of Morph.");
        }

        #endregion

        private readonly Socket _socket;
        public Socket Socket
        {
            get => _socket;
        }

        private readonly DataInHandler _dataIn;
        private const int BufferSize = 2048;

        public EndPoint LocalEndPoint
        {
            get => _socket.LocalEndPoint;
        }

        public EndPoint RemoteEndPoint
        {
            get => _socket.RemoteEndPoint;
        }

        private LinkInternet _link = null;
        public LinkInternet Link
        {
            get
            {
                if (_link == null)
                    _link = LinkInternet.New(RemoteEndPoint);
                return _link;
            }
        }

        public void Write(LinkMessage Mmessage)
        {
            //  Don't write until Morph validation information has been sent
            _morphValidationSent.WaitOne();
            //  Get the data to send.
            //  Alot of optimisation could be done here.
            //  This ought to use BeginSend(), but that'll have to wait for now.
            //  Also, this is where one would batch messages
            //
            //  Create a stream
            MemoryStream stream = new MemoryStream();
            //  Write the Message to the stream
            Mmessage.Write(new MorphWriter(stream));
            //  Start from the beginning
            long totalCount = stream.Length;
            stream.Position = 0;
            //  Write the stream to the socket
            byte[] buffer = new byte[BufferSize];
            lock (_socket)
                if (_socket.Connected)
                    while (totalCount > 0)
                    {
                        int count = (int)(totalCount < BufferSize ? totalCount : BufferSize);
                        count = stream.Read(buffer, 0, count);
                        _socket.Send(buffer, 0, count, SocketFlags.Partial);
                        totalCount -= count;
                    }
        }

        #region Receiving

        private readonly byte[] _incomingData = new byte[BufferSize];

        internal void StartReceivingData()
        {
            new Thread(new ThreadStart(ReceivingData)).Start();
        }

        private void ReceivingData()
        {
            Thread.CurrentThread.Name = "Connection";
            try
            {
                //  Validate the connection
                TestMorphValidation();
                //  Read incoming data
                int noDataIteration = 0;
                while (_socket.Connected)
                {
                    int count = _socket.Receive(_incomingData, 0, _incomingData.Length, SocketFlags.Partial);
                    //  Workaround for _Socket.Receive() continuously returning 0 instead of waiting for data.
                    //  Seems to happen when connection has been abandoned by remote host.
                    if (count > 0)
                        noDataIteration = 0;
                    else if (noDataIteration++ > 5)
                        Close();
                    //  Add the received data to the message stream
                    _dataIn.AddData(_incomingData, 0, count);
                }
            }
            catch (Exception x)
            {
                //  Whatever the problem, make sure the thread is closed
                try
                {
                    Close();
                }
                catch (Exception)
                {
                }
                MorphErrors.NotifyAbout(this, x);
            }
        }

        #endregion

        public void Close()
        {
            //  Prevent other threads from writing to the socket
            Connections.Remove(this);
            lock (_socket)
            {
                //  Notify attached objects to tidy themselves up
                OnClose?.Invoke(this, new EventArgs());
                //  Try to tell other end that the socket is closing
                if (_socket.Connected)
                {
                    try
                    {
                        _socket.Send(new byte[] { 0 }); //  Sending LinkEnd
                    }
                    catch
                    {
                    }
                    //  Now close the socket
                    _socket.Close();
                }
            }
        }

        #region RegisterItemName Members

        private readonly string _name;
        public string Name
        {
            get => _name;
        }

        #endregion

        public event EventHandler OnClose;
    }

    static public class Connections
    {
        #region internal

        private static readonly RegisterItems<Connection> s_Conns = new RegisterItems<Connection>();
        private static readonly Hashtable s_LocalEndPoints = new Hashtable();

        private class LocalEndPointCounter
        {
            public int count = 1;
        }

        static private IPAddress[] CurrentLocalAddresses()
        {
            return Dns.GetHostEntry(Dns.GetHostName()).AddressList;
        }

        static private Socket NewSocket(IPEndPoint remoteEndPoint)
        {
            ProtocolType protocol;
            if (remoteEndPoint.AddressFamily == AddressFamily.InterNetwork)
                protocol = ProtocolType.IP;
            else if (remoteEndPoint.AddressFamily == AddressFamily.InterNetworkV6)
                protocol = ProtocolType.IPv6;
            else
                throw new Exception("Unsupported protocol");
            //  Create socket connection
            Socket socket = new Socket(remoteEndPoint.AddressFamily, SocketType.Stream, protocol);
            //socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, 1);
            //socket.Bind(new IPEndPoint(IPAddress.Any, 0));
            socket.Connect(remoteEndPoint);
            //  Create connection
            return socket;
        }

        static internal void Add(Connection connection)
        {
            lock (s_Conns)
            {
                s_Conns.Add(connection);
                //  If this is a server process, then multiple sockets may use the same local endpoint,
                //  so keep a reference count.
                lock (s_LocalEndPoints)
                {
                    LocalEndPointCounter counter = (LocalEndPointCounter)s_LocalEndPoints[connection.LocalEndPoint.ToString()];
                    if (counter == null)
                        s_LocalEndPoints.Add(connection.LocalEndPoint.ToString(), new LocalEndPointCounter());
                    else
                        counter.count++;
                }
            }
        }

        static internal void Remove(Connection connection)
        {
            lock (s_Conns)
            {
                s_Conns.Remove(connection);
                lock (s_LocalEndPoints)
                {
                    LocalEndPointCounter counter = (LocalEndPointCounter)s_LocalEndPoints[connection.LocalEndPoint.ToString()];
                    if (counter != null)  //  Should not be possible to be null, but just just in case
                    { //  Decrement the refence count
                        counter.count--;
                        //  If there are no more, then remove the counter
                        if (counter.count == 0)
                            s_LocalEndPoints.Remove(connection.LocalEndPoint.ToString());
                    }
                }
            }
        }

        #endregion

        static public Connection Add(Socket socket)
        {
            if (socket == null)
                throw new EMorphUsage("Cannot connect with a null connection");
            //  Create (and register) the new connection
            return new Connection(socket);
        }

        static public Connection Add(IPEndPoint remoteEndPoint)
        {
            if (remoteEndPoint == null)
                throw new EMorphUsage("Cannot connect to a null end point");
            //  Create (and register) the new connection
            return Add(NewSocket(remoteEndPoint));
        }

        static public Connection Obtain(IPEndPoint remoteEndPoint)
        {
            Connection connection = Find(remoteEndPoint);
            if (connection == null)
                return new Connection(NewSocket(remoteEndPoint));
            return connection;
        }

        static public Connection Find(IPEndPoint remoteEndPoint)
        {
            lock (s_Conns)
                return s_Conns.Find(remoteEndPoint.ToString());
        }

        static public void CloseAll()
        {
            List<Connection> allConns;
            lock (s_Conns)
                allConns = s_Conns.List();
            foreach (Connection conn in allConns)
                conn.Close();
        }

        static public bool IsEndPointOnThisDevice(IPEndPoint endPoint)
        {
            IPAddress[] localAddresses = CurrentLocalAddresses();
            foreach (IPAddress localAddress in localAddresses)
                if (endPoint.Address.Equals(localAddress))
                    return true;
            return IPAddress.Loopback.Equals(endPoint.Address);
        }

        static public bool IsEndPointOnThisProcess(IPEndPoint endPoint)
        {
            lock (s_LocalEndPoints)
                return s_LocalEndPoints[endPoint.ToString()] != null;
        }
    }
}