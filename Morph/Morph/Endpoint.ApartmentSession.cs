using System;
using System.Collections.Generic;
using System.Threading;
using Morph.Base;
using Morph.Core;
using Morph.Lib.LinkedList;
using Morph.Params;
using Morph.Sequencing;

namespace Morph.Endpoint
{
    public class MorphApartmentSession : MorphApartment, IDisposable, IActionLinkSequence
    {
        internal protected MorphApartmentSession(MorphApartmentFactory owner, object defaultObject, SequenceLevel level)
          : base(owner, owner.InstanceFactories, defaultObject)
        {
            if (level != SequenceLevel.None)
                _sequence = SequenceReceivers.New(level == SequenceLevel.Lossless);
            _linkApartment = new LinkApartment(ID);
        }

        #region IDisposable Members

        public override void Dispose()
        {
            base.Dispose();
            _sequence?.Stop(true);
        }

        #endregion

        internal DateTime _when;
        internal IBookmark _bookmark = null;

        public override void ResetTimeout()
        {
            ((MorphApartmentFactorySession)Owner).ResetTimeout(this);
        }

        private readonly LinkApartment _linkApartment;
        private LinkStack _path = null;
        public LinkStack Path
        {
            get => _path;
            set
            {
                if (value != null)
                    lock (this)
                    {
                        _path = EndpointPathOf(value);
                        //  Apply path to Sequence
                        if (_sequence != null)
                            _sequence.PathToProxy = _path;
                    }
            }
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

        private readonly SequenceReceiver _sequence;
        public SequenceLevel SequenceLevel
        {
            get
            {
                if (_sequence == null)
                    return SequenceLevel.None;
                if (_sequence.IsLossless)
                    return SequenceLevel.Lossless;
                else
                    return SequenceLevel.Lossy;
            }
        }

        internal LinkStack GenerateReturnPath()
        {
            LinkStack ReturnPath = _path.Clone();
            ReturnPath.Push(_linkApartment);
            if (SequenceLevel != SequenceLevel.None)
                ReturnPath.Append(_sequence.StartLink());
            return ReturnPath;
        }

        #region IActionLinkSequence

        public override void ActionLinkSequence(LinkMessage message, LinkSequence linkSequence)
        {
            linkSequence.Action(message);
        }

        #endregion
    }

    public class MorphApartmentFactorySession : MorphApartmentFactory, IDisposable
    {
        public MorphApartmentFactorySession(IDefaultServletObjectFactory defaultServletObject, InstanceFactories instanceFactories, TimeSpan timeout, SequenceLevel sequenceLevel)
          : base(instanceFactories)
        {
            _defaultServletObjectFactory = defaultServletObject;
            _timeout = timeout;
            _sequenceLevel = sequenceLevel;
            Thread timeoutThread = new Thread(new ThreadStart(ThreadExecute)) { IsBackground = true };
            timeoutThread.Start();
        }

        #region IDisposable Members

        public void Dispose()
        {
            _threadRunning = false;
            _threadWait.Set();
            _timeouts.Dispose();
        }

        #endregion

        #region Timeouts

        private bool _threadRunning = true;
        private readonly AutoResetEvent _threadWait = new AutoResetEvent(false);
        private TimeSpan _timeout;
        private readonly LinkedListTwoWay<MorphApartmentSession> _timeouts = new LinkedListTwoWay<MorphApartmentSession>();

        private void ThreadExecute()
        {
            Thread.CurrentThread.Name = "ApartmentFactorySession";
            while (_threadRunning)
            {
                //  Are there any apartments to wait for?
                MorphApartmentSession apartment;
                lock (_timeouts)
                    apartment = _timeouts.PeekLeft();
                //  No, so wait until triggered
                if (apartment == null)
                    _threadWait.WaitOne();
                else
                { //  Might need to wait
                    TimeSpan wait = apartment._when.Subtract(DateTime.Now);
                    if (wait > TimeSpan.Zero)
                        _threadWait.WaitOne(wait, false);
                    else
                    { //  Timed out, so remove and dispose the apartment
                        apartment.Dispose();
                        lock (_timeouts)
                            _timeouts.Pop(apartment._bookmark);
                    }
                }
            }
        }

        internal void ResetTimeout(MorphApartmentSession apartment)
        {
            apartment._when = DateTime.Now.Add(_timeout);
            lock (_timeouts)
                _timeouts.MoveToRightEnd(apartment._bookmark);
        }

        #endregion

        private IDefaultServletObjectFactory _defaultServletObjectFactory;

        protected virtual MorphApartmentSession CreateApartment(object defaultObject, SequenceLevel level)
        {
            return new MorphApartmentSession(this, defaultObject, level);
        }

        public override MorphApartment ObtainDefault()
        {
            //  Create session apartment
            object defaultServletObject = _defaultServletObjectFactory.ObtainServlet();
            if (defaultServletObject == null)
                throw new EMorphUsage("Cannot create an apartment without a default service object");
            MorphApartmentSession apartment = CreateApartment(defaultServletObject, _sequenceLevel);
            if (defaultServletObject is IMorphReference morphReference)
                morphReference.MorphApartment = apartment;
            //  Track timeout
            apartment._when = DateTime.Now.Add(_timeout);
            lock (_timeouts)
            {
                if (!_timeouts.HasData)
                    _threadWait.Set();
                apartment._bookmark = _timeouts.PushRight(apartment);
            }
            return apartment;
        }

        protected internal override void ShutDown()
        {
            base.ShutDown();
            //  If you wish to add in special shut down code for the service, 
            //  then you can make your own MorphApartment factory by extending any
            //  of the classes MorphApartmentFactory, ApartmentsShared, ApartmentsSession.
        }

        private SequenceLevel _sequenceLevel;
        public SequenceLevel SequenceLevel
        {
            get => _sequenceLevel;
        }
    }
}