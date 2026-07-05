/**
 * A thread might want to wait until another thread completes some action.
 * The waiting thread would then call Wait(ID), 
 * and the other thread then calls End(ID) to release the waiting thread from the wait.
 * 
 * The waiting thread may prepare a wait by calling Prepare(ID).  
 * If the waiting thread does not call Wait(ID), then it *must* call Unprepare(ID).
 * 
 * The control thread can control Hold(ID) to stall the waiting thread for as long as necessary.
 * If the control thread calls Hold(ID), then it *must* call End(ID).
 */

using System;
using System.Threading;
using Morph.Lib;

namespace Morph.Endpoint
{
    public class NumberedWaits// : IDisposable
    {
        /*
        ~NumberedWaits()
        {
          Dispose();
        }

        #region IDisposable Members

        public void Dispose()
        {
          foreach (NumberedWait Wait in _Waits)
            End(Wait.ID);
        }

        #endregion
        */

        #region Internals

        private class NumberedWait : IRegisterItemID
        {
            internal NumberedWait(int id)
            {
                _id = id;
            }

            #region RegisterItemID Members

            private int _id;
            public int ID
            {
                get => _id;
            }

            #endregion

            internal AutoResetEvent _gate = new AutoResetEvent(false);
            internal bool _hold = false;
            internal bool _held = false;
            internal bool _abandoned = false;
        }

        private RegisterItems<NumberedWait> _waits = new RegisterItems<NumberedWait>();

        private bool Find(int id, out NumberedWait wait)
        {
            lock (_waits)
                wait = _waits.Find(id);
            return wait != null;
        }

        private NumberedWait Obtain(int id)
        {
            NumberedWait wait;
            if (!Find(id, out wait))
            { //  Doesn't already exist, so create and register a new wait
                wait = new NumberedWait(id);
                lock (_waits)
                    _waits.Add(wait);
            }
            return wait;
        }

        #endregion

        #region For waiting thread

        public void Prepare(int id)
        {
            Obtain(id);
        }

        public void Unprepare(int id)
        {
            lock (_waits)
                _waits.Remove(id);
        }

        public bool Wait(int id, TimeSpan timeout)
        {
            //  Obtain the wait
            NumberedWait wait = Obtain(id);
            try
            {
                //  Wait
                bool result = wait._gate.WaitOne(timeout, false);
                lock (wait)
                    //  No reply is being assigned, so give up.  Marking abandoned (under the same lock
                    //  that Hold() takes) stops a Hold() that is about to run from assigning a reply we
                    //  will never collect.
                    if (!wait._hold)
                    {
                        wait._abandoned = true;
                        return result || wait._held;
                    }
                //  A reply is being assigned;  wait for End(), which follows AssignReply()
                wait._gate.WaitOne();
                return true;
            }
            finally
            {
                //  Done, so deregister the wait
                Unprepare(id);
            }
        }

        #endregion

        #region For control thread

        public bool Hold(int id)
        {
            NumberedWait wait;
            if (!Find(id, out wait))
                return false;
            lock (wait)
            {
                //  The waiting thread has already given up, so there is no one to hold for
                if (wait._abandoned)
                    return false;
                wait._hold = true;
                wait._held = true;
                return true;
            }
        }

        public void End(int id)
        {
            NumberedWait wait;
            if (Find(id, out wait))
            {
                wait._hold = false; //  Don't hold
                wait._gate.Set();   //  Release it
            }
        }

        #endregion
    }
}