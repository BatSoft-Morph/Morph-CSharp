using System;
using System.Collections;
using System.IO;
using System.Threading;

namespace Morph.Core
{
    public class MorphStream : Stream, IDisposable
    {
        #region IDisposable

        void IDisposable.Dispose()
        {
            lock (_queue)
                if (_totalRemaining >= 0)
                {
                    _totalRemaining = -1;
                    _queue.Clear();
                    _gate.Set();
                }
        }

        #endregion

        #region Internal

        private readonly Queue _queue = new Queue();
        private long _totalRemaining = 0;
        private int _waitingFor = 0;

        private void WaitFor(int count)
        {
            //  If not enough data is available, then we wait
            lock (_queue)
                if (count <= _totalRemaining)
                    return;
                else
                    _waitingFor = count;
            _gate.WaitOne();
            //  Stream may no longer be valid
            if (_totalRemaining < 0)
                throw new ObjectDisposedException("MorphStream is disposed");
        }

        private int _segmentPos = 0;
        private byte[] _segment = new byte[0];

        private readonly ManualResetEvent _gate = new ManualResetEvent(false);

        private byte[] Segment()
        {
            lock (_gate)
            {
                if (_segmentPos == _segment.Length)
                {
                    _segment = (byte[])_queue.Dequeue();
                    _segmentPos = 0;
                }
                return _segment;
            }
        }

        #endregion

        public long Remaining
        {
            get => _totalRemaining;
        }

        public byte Peek()
        {
            if (_totalRemaining < 0)
                throw new ObjectDisposedException("MorphStream is disposed");
            WaitFor(1);
            lock (_segment)
                return Segment()[_segmentPos];
        }

        public byte[] Read(int count)
        {
            WaitFor(count);
            //  Read the data
            byte[] result = new byte[count];
            if (Read(result, 0, result.Length) < count)
                throw new EMorphImplementation();
            return result;
        }

        #region Stream implementation

        public override bool CanRead
        {
            get => true;
        }

        public override bool CanSeek
        {
            get => false;
        }

        public override bool CanWrite
        {
            get => true;
        }

        public override void Flush()
        {
            //  Writes go straight into the queue, so there is nothing buffered to flush.
            //  (Clearing the queue here would discard received-but-unread data.)
        }

        public override long Length
        {
            get { throw new NotSupportedException(); }
        }

        public override long Position
        {
            get { throw new NotSupportedException(); }
            set { throw new NotSupportedException(); }
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (buffer == null)
                throw new ArgumentNullException();
            if ((offset < 0) || (count < 0))
                throw new ArgumentOutOfRangeException();
            if (buffer.Length < offset + count)
                throw new ArgumentException();
            if (_totalRemaining < 0)
                throw new ObjectDisposedException("MorphStream is disposed");
            //  If no data is available, then we must wait
            _gate.WaitOne();
            //  Can't copy more than we have
            if (_totalRemaining < count)
                count = (int)_totalRemaining;
            int result = count;
            //  Might have to copy from several segments
            while (count > 0)
                lock (_segment)
                {
                    //  Might have to "page" to next segment
                    Segment();
                    //  Determine copy count for this segment
                    int copyCount = _segment.Length - _segmentPos;
                    if (copyCount > count)
                        copyCount = count;
                    //  Copy from segment
                    lock (_gate)
                    {
                        Array.Copy(_segment, _segmentPos, buffer, offset, copyCount);
                        offset += copyCount;
                        _totalRemaining -= copyCount;
                        _segmentPos += copyCount;
                        if (_totalRemaining == 0)
                            _gate.Reset();
                    }
                    count -= copyCount;
                }
            return result;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            if (buffer.Length < offset + count)
                throw new ArgumentException();
            if (count == 0)
                return;
            //  Copy data
            byte[] newSegment = new byte[count];
            Array.Copy(buffer, offset, newSegment, 0, newSegment.Length);
            //  Add data to queue
            lock (_gate)
            {
                _queue.Enqueue(newSegment);
                _totalRemaining += newSegment.Length;
                _gate.Set();
            }
        }

        #endregion
    }
}