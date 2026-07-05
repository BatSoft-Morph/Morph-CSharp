using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using Morph.Base;
using Morph.Core;
using Morph.Lib;

namespace Morph.Sequencing
{
    public class SequenceReceiver : IDisposable
    {
        internal SequenceReceiver(int sequenceID, bool isLossless)
        {
            _sequenceID = sequenceID;
            IsLossless = isLossless;
            lock (SequenceReceivers.s_all)
                SequenceReceivers.s_all.Add(sequenceID, this);
            Start();
        }

        #region IDisposable Members

        public void Dispose()
        {
            //  Stop the execution thread, then deregister
            Stop(true);
            lock (SequenceReceivers.s_all)
                SequenceReceivers.s_all.Remove(_sequenceID);
        }

        #endregion

        private int _sequenceID;
        public int SequenceID
        {
            get => _sequenceID;
        }

        private int _senderID = 0;
        public int SenderID
        {
            get => _senderID;
            set => _senderID = value;
        }

        private ISequenceImplementation _sequenceImplementation = null;
        public bool IsLossless
        {
            get => _sequenceImplementation is ImplLossless;
            set
            {
                if (_sequenceImplementation != null)
                    if (IsLossless == value)
                        return;
                    else if (!value)
                        throw new EMorphUsage("Sequence cannot change from lossless to lossy.");
                lock (_queue)
                    if (_currentIndex == 0)
                        if (value)
                            _sequenceImplementation = new ImplLossless(this);
                        else
                            _sequenceImplementation = new ImplLossy(this);
                    else
                        throw new EMorphUsage("Sequence has already started.");
            }
        }

        private LinkStack _PathToProxy;
        public LinkStack PathToProxy
        {
            get => _PathToProxy;
            set => _PathToProxy = value;
        }

        private TimeSpan _timeout = SequenceReceivers.DefaultTimeout;
        public TimeSpan Timeout
        {
            get => _timeout;
            set
            {
                if (value != null)
                    _timeout = value;
            }
        }

        internal void Index(int index, LinkMessage message)
        {
            _sequenceImplementation.Add(index, message);
        }

        internal void Stop(int index)
        {
            //  'index' is the final index in the sequence;  stop gracefully once the queue has drained
            Stop(false);
        }

        private void SendReply(LinkSequence linkReply)
        {
            try
            {
                LinkMessage reply = new LinkMessage(_PathToProxy.Clone(), null, true);
                reply.PathTo.Append(linkReply);
                reply.NextLinkAction();
            }
            catch (Exception x)
            {
                MorphErrors.NotifyAbout(x);
            }
        }

        #region Thread control

        private bool _isStopped = false;
        private bool _isStoppedForce;
        private int _currentIndex = 0;
        private readonly AutoResetEvent _gate = new AutoResetEvent(false);
        private readonly Hashtable _queue = new Hashtable();

        private void Start()
        {
            new Thread(new ThreadStart(ExecutionMethod)).Start();
        }

        public void Stop(bool force)
        {
            _isStoppedForce = force;
            _isStopped = true;
            lock (_queue)
                _gate.Set();
        }

        protected void ExecutionMethod()
        {
            Thread.CurrentThread.Name = "Sequence";
            while (_sequenceImplementation.ExecutionIteration()) ;
            //  Thread is ending, so make sure the receiver is deregistered
            lock (SequenceReceivers.s_all)
                SequenceReceivers.s_all.Remove(_sequenceID);
        }

        private interface ISequenceImplementation
        {
            bool ExecutionIteration();
            void Add(int newIndex, LinkMessage message);
        }

        #region Lossy

        private class ImplLossy : ISequenceImplementation
        {
            internal ImplLossy(SequenceReceiver owner)
            {
                _Owner = owner;
            }

            private readonly SequenceReceiver _Owner;

            public bool ExecutionIteration()
            {
                //  Might be time to stop
                if (_Owner._isStopped)
                    lock (_Owner._queue)
                        if (_Owner._isStoppedForce || (_Owner._queue.Count == 0))
                            return false;
                //  Get next item
                object item = null;
                lock (_Owner._queue)
                    if (_Owner._queue.Count > 0)
                    {
                        while (item == null)
                            item = _Owner._queue[++_Owner._currentIndex];
                        _Owner._queue.Remove(_Owner._currentIndex);
                    }
                //  Handle empty queue
                if (item == null)
                    _Owner._gate.WaitOne();
                //  Handle message
                else
                    try
                    {
                        ((LinkMessage)item).NextLinkAction();
                    }
                    catch (Exception x)
                    {
                        MorphErrors.NotifyAbout(x);
                    }
                return true;
            }

            public void Add(int newIndex, LinkMessage message)
            {
                lock (_Owner._queue)
                {
                    if (_Owner._currentIndex < newIndex)
                        _Owner._queue[newIndex] = message;
                    //  Make sure the execution thread is awake
                    _Owner._gate.Set();
                }
            }
        }

        #endregion

        #region Lossless

        private class ImplLossless : ISequenceImplementation
        {
            internal ImplLossless(SequenceReceiver owner)
            {
                _Owner = owner;
            }

            private readonly SequenceReceiver _Owner;
            private int _FurthestIndex = 1;

            public bool ExecutionIteration()
            {
                //  Might be time to stop
                if (_Owner._isStopped)
                    lock (_Owner._queue)
                        if (_Owner._isStoppedForce || (_Owner._queue.Count == 0))
                            return false;
                //  Get next item
                object item = null;
                lock (_Owner._queue)
                    if (_Owner._queue.Count > 0)
                        item = _Owner._queue[_Owner._currentIndex];
                //  Handle empty queue
                if (item == null)
                {
                    _Owner._gate.WaitOne();
                    return true;
                }
                //  Handle message
                if (item is LinkMessage)
                {
                    try
                    {
                        _Owner._queue.Remove(_Owner._currentIndex);
                        _Owner._currentIndex++;
                        ((LinkMessage)item).NextLinkAction();
                    }
                    catch (Exception x)
                    {
                        MorphErrors.NotifyAbout(x);
                    }
                    return true;
                }
                //  Handle timeout
                if (item is DateTime)
                {
                    //  Send Resend requests for those messages that have timed out
                    List<int> Resends = new List<int>();
                    DateTime EarliestDue = DateTime.MaxValue;
                    lock (_Owner._queue)
                        for (int i = _Owner._currentIndex; i < _FurthestIndex; i++)
                        {
                            item = _Owner._queue[i];
                            if (!(item is DateTime))
                                break;
                            DateTime dueAt = (DateTime)item;
                            //  If it was due before now then...
                            if (dueAt.CompareTo(DateTime.Now) < 0)
                            {
                                //  ...update the due time
                                dueAt = dueAt.Add(_Owner._timeout);
                                _Owner._queue[i] = dueAt;
                                //  ...make a note to send Resend request
                                Resends.Add(i);
                            }
                            if (dueAt.CompareTo(EarliestDue) < 0)
                                EarliestDue = dueAt;
                        }
                    //  Send Resend requests (outside the lock)
                    for (int i = 0; i < Resends.Count; i++)
                        _Owner.SendReply(new LinkSequenceIndexReply(_Owner._senderID, Resends[i], true));
                    //  Wait for the earliest timeout to expire
                    //  Note: if EarliestDue = MaxValue, then _Gate will also be set, yielding no wait.
                    TimeSpan waitTime = EarliestDue.Subtract(DateTime.Now);
                    if (waitTime > TimeSpan.Zero)
                        _Owner._gate.WaitOne(waitTime, false);
                }
                return true;
            }

            public void Add(int newIndex, LinkMessage message)
            {
                if (_Owner._currentIndex == 0)
                    _Owner._currentIndex = 1;
                lock (_Owner._queue)
                    //  May be a resend for a message that's already been digested
                    if (_Owner._currentIndex <= newIndex)
                    {
                        //  Add the message into the queue. (Replacing an existing message is fine.)
                        _Owner._queue[newIndex] = message;
                        //  Update the furthest index
                        if (_FurthestIndex < newIndex)
                        {
                            _FurthestIndex++;
                            //  Fill in any Lossy with "expected by" times
                            if (_FurthestIndex < newIndex)
                            {
                                DateTime ExpectedBy = DateTime.Now.Add(_Owner.Timeout);
                                do
                                {
                                    _Owner._queue[_FurthestIndex] = ExpectedBy;
                                    _FurthestIndex++;
                                } while (_FurthestIndex < newIndex);
                            }
                        }
                        //  Make sure the execution thread is awake
                        _Owner._gate.Set();
                    }
                //  Send Ack
                _Owner.SendReply(new LinkSequenceIndexReply(_Owner._senderID, newIndex, false));
            }
        }

        #endregion

        #endregion

        public Link StartLink()
        {
            lock (_queue)
                if (_currentIndex == 0)
                    return new LinkSequenceStartReply(_sequenceID, _senderID, IsLossless);
                else
                    return null;
        }
    }

    public static class SequenceReceivers
    {
        static internal Hashtable s_all = new Hashtable();
        private static readonly IDSeed _SequenceIDSeed = new IDSeed();

        static internal SequenceReceiver Find(int sequenceID)
        {
            lock (s_all)
                return (SequenceReceiver)s_all[sequenceID];
        }

        static public SequenceReceiver New(bool isLossless)
        {
            lock (s_all)
            {
                int sequenceID;
                do
                {
                    sequenceID = _SequenceIDSeed.Generate();
                } while (s_all.Contains(sequenceID));
                return new SequenceReceiver(sequenceID, isLossless);
            }
        }

        static public TimeSpan DefaultTimeout = new TimeSpan(0, 0, 5);
    }
}