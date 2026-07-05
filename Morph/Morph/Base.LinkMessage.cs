using System;
using Morph.Core;

namespace Morph.Base
{
    public class LinkMessage : Link
    {
        public LinkMessage(LinkStack pathTo, LinkStack pathFrom, bool isForceful)
          : base(LinkTypeID.Message)
        {
            _pathTo = pathTo;
            _pathFrom = pathFrom;
            _isForceful = isForceful;
        }

        internal protected LinkMessage(MorphReader reader)
          : base(LinkTypeID.Message)
        {
            //  Read link byte
            bool hasPathFrom;
            reader.ReadLinkByte(out _hasCallNumber, out _isForceful, out hasPathFrom);
            //  Read link values
            //  - CallNumber
            if (_hasCallNumber)
                _callNumber = reader.ReadInt32();
            //  - PathTo size
            int pathToSize = reader.ReadInt32();
            //  - PathFrom size
            int pathFromSize = 0;
            if (hasPathFrom)
                pathFromSize = reader.ReadInt32();
            //  Paths
            _pathTo = new LinkStack(reader.SubReader(pathToSize));
            if (hasPathFrom)
                _pathFrom = new LinkStack(reader.SubReader(pathFromSize));
        }

        private bool _hasCallNumber = false;
        public bool HasCallNumber
        {
            get => _hasCallNumber;
        }

        private int _callNumber;
        public int CallNumber
        {
            get
            {
                if (!_hasCallNumber)
                    throw new EMorphImplementation();
                return _callNumber;
            }
            set
            {
                _callNumber = value;
                _hasCallNumber = true;
            }
        }

        private bool _isForceful;
        public bool IsForceful
        {
            get => _isForceful;
            set => _isForceful = value;
        }

        private LinkStack _pathTo = null;
        public LinkStack PathTo
        {
            get => _pathTo;
        }

        public bool HasPathFrom
        {
            get => _pathFrom != null;
        }

        private LinkStack _pathFrom = null;
        public LinkStack PathFrom
        {
            get => _pathFrom;
        }

        public object Source = null;  //  The connection that the message comes from

        public object Context = null;

        public bool ContextIs(Type contextType)
        {
            return (Context != null) && contextType.IsInstanceOfType(Context);
        }

        public Link Current
        {
            get
            {
                if (_pathTo == null)
                    return null;
                else
                    return _pathTo.Peek();
            }
        }

        public bool CurrentIs(LinkTypeID linkTypeID)
        {
            Link currentLink = Current;
            return (currentLink != null) && (currentLink.LinkTypeID == linkTypeID);
        }

        public void NextLink()
        {
            if (_pathFrom != null)
                _pathFrom.Push(_pathTo.Pop());
            else
                _pathTo.Pop();
        }

        public void NextLinkAction()
        {
            NextLink();
            LinkTypes.ActionCurrentLink(this);
        }

        public void Action()
        {
            LinkTypes.ActionCurrentLink(this);
        }

        #region Link

        public override int Size()
        {
            long totalSize = 1;
            //  Call number
            if (HasCallNumber)
                totalSize += 4;
            //  Path To size
            totalSize += 4 + PathTo.ByteSize;
            //  Path From size
            if (HasPathFrom)
                totalSize += 4 + PathFrom.ByteSize;
            //  Analyze limits
            if (totalSize > int.MaxValue)
                throw new EMorph("Message is too large");
            return (int)totalSize;
        }

        public override void Write(MorphWriter writer)
        {
            lock (writer)
            {
                //  Message link byte
                writer.WriteLinkByte(LinkTypeID, _hasCallNumber, _isForceful, HasPathFrom);
                //  Call number
                if (_hasCallNumber)
                    writer.WriteInt32(CallNumber);
                //  Path to size
                writer.WriteInt32(PathTo.ByteSize);
                //  Path from size
                if (HasPathFrom)
                    writer.WriteInt32(PathFrom.ByteSize);
                //  Path to
                PathTo.Write(writer);
                //  Path from
                if (HasPathFrom)
                    PathFrom.Write(writer);
            }
        }

        #endregion

        public LinkMessage Clone()
        {
            LinkMessage clone = new LinkMessage(PathTo, PathFrom, _isForceful);
            clone._hasCallNumber = _hasCallNumber;
            clone._callNumber = _callNumber;
            if (_pathTo != null)
                clone._pathTo = _pathTo.Clone();
            if (_pathFrom != null)
                clone._pathFrom = _pathFrom.Clone();
            return clone;
        }

        public LinkMessage CreateReply()
        {
            if (!HasPathFrom)
                return null;
            LinkMessage reply = new LinkMessage(PathFrom.Clone(), new LinkStack(), _isForceful);
            if (HasCallNumber)
                reply.CallNumber = CallNumber;
            reply.IsForceful = IsForceful;
            return reply;
        }

        public LinkMessage CreateReply(Exception x)
        {
            if (!HasPathFrom)
                return null;
            LinkMessage reply = new LinkMessage(PathFrom.Clone(), null, _isForceful);
            if (HasCallNumber)
                reply.CallNumber = CallNumber;
            reply.IsForceful = IsForceful;
            reply.PathTo.Append(new LinkData(x));
            return reply;
        }

        public LinkMessage CreateReply(Link payload)
        {
            LinkMessage reply = CreateReply();
            if (reply == null)
                return null;
            reply.PathTo.Append(payload);
            return reply;
        }

        public LinkMessage CreateReply(LinkStack payload)
        {
            LinkMessage reply = CreateReply();
            if (reply == null)
                return null;
            reply.PathTo.Append(payload);
            return reply;
        }

        public override bool Equals(object obj)
        {
            return base.Equals(obj);
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }

        public override string ToString()
        {
            string str = "{Message";
            if (HasCallNumber)
                str += " CallNumber=" + CallNumber.ToString();
            if (_isForceful)
                str += " IsForceful";
            str += " To=" + PathTo.ToString();
            if (HasPathFrom)
                str += " From=" + PathFrom.ToString();
            return str + '}';
        }
    }

    public class LinkTypeMessage : ILinkTypeReader, ILinkTypeAction
    {
        public LinkTypeID ID
        {
            get => LinkTypeID.Message;
        }

        public Link ReadLink(MorphReader Reader)
        {
            return new LinkMessage(Reader);
        }

        public void ActionLink(LinkMessage Message, Link CurrentLink)
        {
            throw new NotImplementedException();
        }
    }
}