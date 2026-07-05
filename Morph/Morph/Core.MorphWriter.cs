using System.IO;
using System.Text;

namespace Morph.Core
{
    public class MorphWriter
    {
        public MorphWriter(MemoryStream stream)
        {
            _stream = stream;
        }

        private static readonly Encoding ASCII = Encoding.ASCII;
        private static readonly Encoding Unicode = Encoding.UTF8;

        private readonly MemoryStream _stream;
        private const int BufferSize = 256;

        public Stream Stream
        {
            get => _stream;
        }

        static public int SizeOfString(string chars)
        {
            return Unicode.GetByteCount(chars);
        }

        #region Link byte

        private const byte Zero = 0x00;
        private const byte BitX = 0x10;
        private const byte BitY = 0x20;
        private const byte BitZ = 0x40;
        private const byte BitMSB = 0x80;

        static public byte EncodeLinkByte(LinkTypeID linkTypeID, bool x, bool y, bool z)
        {
            return (byte)(BitMSB | (z ? BitZ : Zero) | (y ? BitY : Zero) | (x ? BitX : Zero) | (byte)linkTypeID);
        }

        public void WriteLinkByte(LinkTypeID linkTypeID, bool x, bool y, bool z)
        {
            WriteInt8(BitMSB | (z ? BitZ : Zero) | (y ? BitY : Zero) | (x ? BitX : Zero) | (byte)linkTypeID);
        }

        #endregion

        public bool MSB
        {
            get => true;
        }

        public void WriteInt8(int value)
        {
            _stream.WriteByte((byte)value);
        }

        public void WriteInt16(int value)
        {
            _stream.WriteByte((byte)(value >> 8));
            _stream.WriteByte((byte)(value));
        }

        public void WriteInt32(int value)
        {
            _stream.WriteByte((byte)(value >> 24));
            _stream.WriteByte((byte)(value >> 16));
            _stream.WriteByte((byte)(value >> 8));
            _stream.WriteByte((byte)(value));
        }

        public void WriteInt64(long value)
        {
            _stream.WriteByte((byte)(value >> 56));
            _stream.WriteByte((byte)(value >> 48));
            _stream.WriteByte((byte)(value >> 40));
            _stream.WriteByte((byte)(value >> 32));
            _stream.WriteByte((byte)(value >> 24));
            _stream.WriteByte((byte)(value >> 16));
            _stream.WriteByte((byte)(value >> 8));
            _stream.WriteByte((byte)(value));
        }

        public void WriteString(string value)
        {
            byte[] buffer = Unicode.GetBytes(value);
            WriteInt32(buffer.Length);
            _stream.Write(buffer, 0, buffer.Length);
        }

        public void WriteIdentifier(string value)
        {
            byte[] buffer = Unicode.GetBytes(value);
            if (buffer.Length > 0xFFFF)
                throw new EMorph("Identifier is too long to encode in a 2 byte length");
            WriteInt16(buffer.Length);
            _stream.Write(buffer, 0, buffer.Length);
        }

        public void WriteChars(string chars, bool asUnicode)
        {
            byte[] buffer;
            if (asUnicode)
                buffer = Unicode.GetBytes(chars);
            else
                buffer = ASCII.GetBytes(chars);
            _stream.Write(buffer, 0, buffer.Length);
        }

        public void WriteBytes(byte[] buffer)
        {
            _stream.Write(buffer, 0, buffer.Length);
        }

        public void WriteStream(MorphReader reader)
        {
            while (reader.Remaining > 0)
                WriteStream(reader, reader.Remaining < int.MaxValue ? (int)reader.Remaining : int.MaxValue);
        }

        public void WriteStream(MorphReader reader, int count)
        {
            //  ReadBytes(chunk) returns exactly 'chunk' bytes or throws EMorph("EOS"), so a reader
            //  with fewer than 'count' bytes raises EOS rather than looping forever.
            while (count > 0)
            {
                int chunk = count < BufferSize ? count : BufferSize;
                byte[] buffer = reader.ReadBytes(chunk);
                _stream.Write(buffer, 0, buffer.Length);
                count -= buffer.Length;
            }
        }

        public byte[] ToArray()
        {
            return _stream.ToArray();
        }
    }
}