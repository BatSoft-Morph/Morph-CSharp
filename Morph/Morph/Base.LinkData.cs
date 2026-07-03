using System;
using Morph.Core;
using Morph.Params;

namespace Morph.Base
{
    public class LinkData : Link
    {
        public LinkData(byte[] Data, bool MSB, bool isException, int errorCode)
          : base(LinkTypeID.Data)
        {
            _data = Data;
            _MSB = MSB;
            _isException = isException;
            _errorCode = errorCode;
        }

        public LinkData(MorphWriter writer)
          : base(LinkTypeID.Data)
        {
            _data = writer.ToArray();
            _MSB = writer.MSB;
            _isException = false;
        }

        public LinkData(Exception x)
          : base(LinkTypeID.Data)
        {
            MorphWriter writer = Parameters.Encode(null, x, s_exceptionInstanceFactory);
            _data = writer.ToArray();
            _MSB = writer.MSB;
            _isException = true;
            if (x is EMorph morph)
                _errorCode = morph.ErrorCode;
            else
                _errorCode = 0;
        }

        private static readonly InstanceFactories s_exceptionInstanceFactory = new InstanceFactories();

        private readonly byte[] _data;
        public byte[] Data
        {
            get => _data;
        }

        private readonly bool _MSB;
        public bool MSB
        {
            get => _MSB;
        }

        private readonly bool _isException;
        public bool IsException
        {
            get => _isException;
        }

        private readonly int _errorCode;
        public int ErrorCode
        {
            get => _errorCode;
        }

        public MorphReader Reader
        {
            get => new MorphReaderSized(_data, _MSB);
        }

        #region Link

        public override int Size()
        {
            return 5 + (_isException ? 4 : 0) + _data.Length;
        }

        public override void Write(MorphWriter writer)
        {
            writer.WriteLinkByte(LinkTypeID, _isException, false, false);
            if (_isException)
                writer.WriteInt32(_errorCode);
            writer.WriteInt32(_data.Length);
            writer.WriteBytes(_data);
        }

        #endregion

        public override bool Equals(object obj)
        {
            return base.Equals(obj);
        }

        public override int GetHashCode()
        {
            return _data.GetHashCode();
        }

        public override string ToString()
        {
            string str = "{Data";
            if (IsException)
            {
                str += " Error";
                if (ErrorCode != 0)
                    str += '=' + ErrorCode.ToString();
            }
            return str + " Length=" + Data.Length.ToString() + '}';
        }
    }

    public interface IActionLinkData
    {
        void ActionLinkData(LinkMessage message, LinkData data);
    }

    public class LinkTypeData : ILinkTypeReader, ILinkTypeAction
    {
        #region LinkType Members

        public LinkTypeID ID
        {
            get => LinkTypeID.Data;
        }

        public Link ReadLink(MorphReader reader)
        {
            bool isException, y, z;
            reader.ReadLinkByte(out isException, out y, out z);
            int errorCode = 0;
            if (isException)
                errorCode = reader.ReadInt32();
            int size = reader.ReadInt32();
            byte[] data = reader.ReadBytes(size);
            return new LinkData(data, reader.MSB, isException, errorCode);
        }

        public void ActionLink(LinkMessage message, Link currentLink)
        {
            if (!message.ContextIs(typeof(IActionLinkData)))
                throw new EMorph("Link type not supported by context");
            ((IActionLinkData)message.Context).ActionLinkData(message, (LinkData)currentLink);
        }

        #endregion
    }
}