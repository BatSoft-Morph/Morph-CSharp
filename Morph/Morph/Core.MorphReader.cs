using System;
using System.Text;

namespace Morph.Core
{
    public abstract class MorphReader
    {
        public abstract MorphReaderSized SubReader(int count);

        static protected Encoding ASCII = Encoding.ASCII;
        static protected Encoding Unicode = Encoding.UTF8;

        private int ReadByteCount(byte byteCountSize)
        {
            switch (byteCountSize)
            {
                case 0: return ReadInt8();
                case 1: return ReadInt16() & 0xFFFF;
                case 2: return ReadInt32();
                case 3:
                    { //  This implementation cannot handle byte lengths greater than int.MaxValue because of limitations in the Stream class.
                        long count = ReadInt64();
                        if ((count < 0) || (count > int.MaxValue))
                            throw new EMorphImplementation();
                        return (int)count;
                    }
                default: throw new EMorphImplementation();
            }
        }

        #region Link byte

        private const byte BitX = 0x10;
        private const byte BitY = 0x20;
        private const byte BitZ = 0x40;
        private const byte BitMSB = 0x80;

        static public LinkTypeID DecodeLinkByte(byte linkByte, out bool x, out bool y, out bool z)
        {
            x = (linkByte & BitX) != 0;
            y = (linkByte & BitY) != 0;
            z = (linkByte & BitZ) != 0;
            return (LinkTypeID)(linkByte & 0x0F);
        }

        private LinkTypeID DecodeLinkByte(byte linkByte, out bool x, out bool y, out bool z, out bool MSB)
        {
            x = (linkByte & BitX) != 0;
            y = (linkByte & BitY) != 0;
            z = (linkByte & BitZ) != 0;
            MSB = (linkByte & BitMSB) != 0;
            return (LinkTypeID)(linkByte & 0x0F);
        }

        public LinkTypeID PeekLinkByte(out bool x, out bool y, out bool z)
        {
            return DecodeLinkByte(PeekInt8(), out x, out y, out z, out _MSB);
        }

        public LinkTypeID ReadLinkByte(out bool x, out bool y, out bool z)
        {
            return DecodeLinkByte(ReadInt8(), out x, out y, out z, out _MSB);
        }

        #endregion

        protected bool _MSB;
        public bool MSB
        {
            get => _MSB;
        }

        public abstract bool CanRead
        { get; }

        public abstract long Remaining
        { get; }

        public abstract byte PeekInt8();

        public abstract byte ReadInt8();

        public virtual Int16 ReadInt16()
        {
            byte b0 = ReadInt8();
            byte b1 = ReadInt8();
            if (_MSB)
                return (Int16)((b0 << 8) | b1);
            else
                return (Int16)((b1 << 8) | b0);
        }

        public virtual Int32 ReadInt32()
        {
            byte b0 = ReadInt8();
            byte b1 = ReadInt8();
            byte b2 = ReadInt8();
            byte b3 = ReadInt8();
            if (_MSB)
                return (b0 << 24) | (b1 << 16) | (b2 << 8) | b3;
            else
                return (b3 << 24) | (b2 << 16) | (b1 << 8) | b0;
        }

        public virtual Int64 ReadInt64()
        {
            byte b0 = ReadInt8();
            byte b1 = ReadInt8();
            byte b2 = ReadInt8();
            byte b3 = ReadInt8();
            byte b4 = ReadInt8();
            byte b5 = ReadInt8();
            byte b6 = ReadInt8();
            byte b7 = ReadInt8();
            if (_MSB)
                return ((long)b0 << 56) | ((long)b1 << 48) | ((long)b2 << 40) | ((long)b3 << 32) | ((long)b4 << 24) | ((long)b5 << 16) | ((long)b6 << 8) | b7;
            else
                return ((long)b7 << 56) | ((long)b6 << 48) | ((long)b5 << 40) | ((long)b4 << 32) | ((long)b3 << 24) | ((long)b2 << 16) | ((long)b1 << 8) | b0;
        }

        public string ReadString()
        {
            return ReadChars(ReadInt32(), true);
        }

        //  ByteCountSize limited to 0..3
        public string ReadString(byte byteCountSize, bool asUnicode)
        {
            return ReadChars(ReadByteCount(byteCountSize), asUnicode);
        }

        public string ReadIdentifier()
        {
            return ReadChars(ReadInt16() & 0xFFFF, true);
        }

        public virtual string ReadChars(int byteCount, bool asUnicode)
        {
            byte[] bytes = ReadBytes(byteCount);
            if (asUnicode)
                return Unicode.GetString(bytes);
            else
                return ASCII.GetString(bytes);
        }

        public abstract byte[] ReadBytes(int count);

        public abstract int ReadBytes(byte[] buffer);
    }

    public class MorphReaderSizeless : MorphReader
    {
        public MorphReaderSizeless(MorphStream stream)
        {
            _stream = stream;
        }

        public override MorphReaderSized SubReader(int count)
        {
            return new MorphReaderSized(_stream.Read(count), _MSB);
        }

        private readonly MorphStream _stream;

        public override bool CanRead
        {
            get => _stream.Remaining > 0;
        }

        public override long Remaining
        {
            get => (long)_stream.Remaining;
        }

        public override byte PeekInt8()
        {
            return _stream.Peek();
        }

        public override byte ReadInt8()
        {
            if (_stream.Remaining <= 0)
                throw new EMorph("EOS");
            return (byte)_stream.ReadByte();
        }

        public override byte[] ReadBytes(int count)
        {
            if (_stream.Remaining < count)
                throw new EMorph("EOS");
            byte[] bytes = new byte[count];
            if (_stream.Read(bytes, 0, count) < count)
                throw new EMorphImplementation();
            return bytes;
        }

        public override int ReadBytes(byte[] buffer)
        {
            return _stream.Read(buffer, 0, buffer.Length);
        }
    }

    public class MorphReaderSized : MorphReader
    {
        public MorphReaderSized(byte[] bytes)
          : this(bytes, 0, bytes.Length)
        {
        }

        internal MorphReaderSized(byte[] bytes, bool MSB)
          : this(bytes, 0, bytes.Length)
        {
            _MSB = MSB;
        }

        private MorphReaderSized(byte[] bytes, int position, int count)
        {
            _bytes = bytes;
            _pos = position;
            _end = position + count;
            if (_bytes.Length < _end)
                throw new EMorphUsage("");
        }

        public override MorphReaderSized SubReader(int count)
        {
            ValidateRead(count);
            try
            {
                return new MorphReaderSized(_bytes, _pos, count);
            }
            finally
            {
                _pos += count;
            }
        }

        private readonly byte[] _bytes;
        private int _pos;
        private readonly int _end;

        public override bool CanRead
        {
            get => _pos < _end;
        }

        public override long Remaining
        {
            get => _end - _pos;
        }

        private void ValidateRead(int count)
        {
            if ((count < 0) || (count > _end - _pos))
                throw new EMorph("EOS");
        }

        public override byte PeekInt8()
        {
            if (_pos >= _end)
                throw new EMorph("EOS");
            return _bytes[_pos];
        }

        public override byte ReadInt8()
        {
            if (_pos >= _end)
                throw new EMorph("EOS");
            return _bytes[_pos++];
        }

        public override Int16 ReadInt16()
        {
            ValidateRead(2);
            byte b0 = _bytes[_pos++];
            byte b1 = _bytes[_pos++];
            if (_MSB)
                return (Int16)((b0 << 8) | b1);
            else
                return (Int16)((b1 << 8) | b0);
        }

        public override Int32 ReadInt32()
        {
            ValidateRead(4);
            byte b0 = _bytes[_pos++];
            byte b1 = _bytes[_pos++];
            byte b2 = _bytes[_pos++];
            byte b3 = _bytes[_pos++];
            if (_MSB)
                return (b0 << 24) | (b1 << 16) | (b2 << 8) | b3;
            else
                return (b3 << 24) | (b2 << 16) | (b1 << 8) | b0;
        }

        public override Int64 ReadInt64()
        {
            ValidateRead(8);
            byte b0 = _bytes[_pos++];
            byte b1 = _bytes[_pos++];
            byte b2 = _bytes[_pos++];
            byte b3 = _bytes[_pos++];
            byte b4 = _bytes[_pos++];
            byte b5 = _bytes[_pos++];
            byte b6 = _bytes[_pos++];
            byte b7 = _bytes[_pos++];
            if (_MSB)
                return ((long)b0 << 56) | ((long)b1 << 48) | ((long)b2 << 40) | ((long)b3 << 32) | ((long)b4 << 24) | ((long)b5 << 16) | ((long)b6 << 8) | b7;
            else
                return ((long)b7 << 56) | ((long)b6 << 48) | ((long)b5 << 40) | ((long)b4 << 32) | ((long)b3 << 24) | ((long)b2 << 16) | ((long)b1 << 8) | b0;
        }

        public override byte[] ReadBytes(int count)
        {
            ValidateRead(count);
            byte[] bytes = new byte[count];
            Array.Copy(_bytes, _pos, bytes, 0, count);
            _pos += count;
            return bytes;
        }

        public override int ReadBytes(byte[] buffer)
        {
            int count = (int)Remaining < buffer.Length ? (int)Remaining : buffer.Length;
            ValidateRead(count);
            Array.Copy(_bytes, _pos, buffer, 0, count);
            _pos += count;
            return count;
        }

        public override string ReadChars(int byteCount, bool asUnicode)
        {
            ValidateRead(byteCount);
            return base.ReadChars(byteCount, asUnicode);
        }
    }
}