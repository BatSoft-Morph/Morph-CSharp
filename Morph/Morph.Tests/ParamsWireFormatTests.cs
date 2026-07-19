using Morph.Core;
using Morph.Params;
using NUnit.Framework;

namespace Morph.Tests
{
    /// <summary>
    /// Pins encoded bytes to the exact layout defined in "Morph Protocol.xlsx"
    /// (tabs "ValueType" and "SimpleType"), independently of the decoder.
    /// </summary>
    [TestFixture]
    public class ParamsWireFormatTests
    {
        #region Helpers

        /// <summary>Encodes a single parameter and returns its bytes, without the leading 4 byte ParamCount.</summary>
        private static byte[] EncodedBytes(object value)
        {
            MorphWriter writer = Parameters.Encode(new object[] { value }, new InstanceFactories());
            byte[] all = writer.ToArray();
            Assert.That(all[0], Is.EqualTo(0));
            Assert.That(all[1], Is.EqualTo(0));
            Assert.That(all[2], Is.EqualTo(0));
            Assert.That(all[3], Is.EqualTo(1));     //  ParamCount = 1, MSB
            byte[] result = new byte[all.Length - 4];
            System.Array.Copy(all, 4, result, 0, result.Length);
            return result;
        }

        #endregion

        [Test]
        public void Null_IsSingleIsNullByte()
            => Assert.That(EncodedBytes(null), Is.EqualTo(new byte[] { 0x80 }));

        [Test]
        public void True_IsBooleanSimpleType0x84()
            //  ValueType IsSimple, SimpleType 0x84 ("The full byte value for boolean is 0x84."), value 0xFF
            => Assert.That(EncodedBytes(true), Is.EqualTo(new byte[] { 0x00, 0x84, 0xFF }));

        [Test]
        public void False_IsBooleanSimpleType0x84()
            => Assert.That(EncodedBytes(false), Is.EqualTo(new byte[] { 0x00, 0x84, 0x00 }));

        [Test]
        public void Int32_IsSignedInteger4Bytes()
            //  SimpleType:  Integer | Size4Bytes | IsSigned = 0x60;  value MSB
            => Assert.That(EncodedBytes(5), Is.EqualTo(new byte[] { 0x00, 0x60, 0x00, 0x00, 0x00, 0x05 }));

        [Test]
        public void Byte_IsUnsignedInteger1Byte()
            //  SimpleType:  Integer | Size1Byte = 0x00
            => Assert.That(EncodedBytes((byte)0xAB), Is.EqualTo(new byte[] { 0x00, 0x00, 0xAB }));

        [Test]
        public void UInt64_IsUnsignedInteger8Bytes()
            //  SimpleType:  Integer | Size8Bytes = 0x30
            => Assert.That(EncodedBytes((ulong)1), Is.EqualTo(new byte[] { 0x00, 0x30, 0, 0, 0, 0, 0, 0, 0, 1 }));

        [Test]
        public void String_IsByteCount4BytesThenUtf8()
            //  SimpleType:  String = 0x03;  ByteCount (4 bytes) + UTF-8
            => Assert.That(EncodedBytes("Hi"), Is.EqualTo(new byte[] { 0x00, 0x03, 0x00, 0x00, 0x00, 0x02, (byte)'H', (byte)'i' }));

        [Test]
        public void Char_IsUtf16TwoBytes()
            //  SimpleType:  Char | CharUTF16 = 0x11
            => Assert.That(EncodedBytes('A'), Is.EqualTo(new byte[] { 0x00, 0x11, 0x00, 0x41 }));

        [Test]
        public void Double_IsFloat8Bytes()
            //  SimpleType:  Float | FloatSize8Bytes = 0x12  (z reinterpreted:  ValueSize = 2^(z+2))
            => Assert.That(EncodedBytes(1.0d)[1], Is.EqualTo(0x12));

        [Test]
        public void Single_IsFloat4Bytes()
            //  SimpleType:  Float | FloatSize4Bytes = 0x02
            => Assert.That(EncodedBytes(1.0f)[1], Is.EqualTo(0x02));

        [Test]
        public void Currency_IsScaledBy10000()
        {
            //  SimpleType:  Currency = 0x0A;  8 bytes, fixed point scaled by 10 000:  1.5 -> 15 000 = 0x3A98
            byte[] bytes = EncodedBytes(1.5m);
            Assert.That(bytes, Is.EqualTo(new byte[] { 0x00, 0x0A, 0, 0, 0, 0, 0, 0, 0x3A, 0x98 }));
        }

        private enum WireColour : byte
        {
            Green = 2,
        }

        [Test]
        public void Enum_CarriesTypeNameAsIdentifier2ByteCount()
        {
            //  SimpleType:  EnumOrdinal | Size1Byte = 0x04;
            //  payload:  enum type name as an identifier (2 byte count), then the ordinal
            byte[] expected = new byte[]
            {
                0x00,
                0x04,
                0x00, 0x0A,
                (byte)'W', (byte)'i', (byte)'r', (byte)'e', (byte)'C', (byte)'o', (byte)'l', (byte)'o', (byte)'u', (byte)'r',
                0x02,
            };
            Assert.That(EncodedBytes(WireColour.Green), Is.EqualTo(expected));
        }

        [Test]
        public void ByteArray_UsesArrayElemTypeAndRawBytes()
        {
            //  ValueType:  IsArray | IsArrayElemType | HasTypeName = 0x51
            //  TypeName "Byte[]" as identifier (2 byte count), elem type bytes 0x00 0x00,
            //  ArrayElemCount (4 bytes), then the raw bytes
            byte[] expected = new byte[]
            {
                0x51,
                0x00, 0x06, (byte)'B', (byte)'y', (byte)'t', (byte)'e', (byte)'[', (byte)']',
                0x00, 0x00,
                0x00, 0x00, 0x00, 0x03,
                0x01, 0x02, 0x03,
            };
            Assert.That(EncodedBytes(new byte[] { 1, 2, 3 }), Is.EqualTo(expected));
        }

        [Test]
        public void NamedParameter_WritesTypeNameBeforeValueName()
        {
            //  A struct member carries its field name as ValueName:
            //  the ValueType byte must have HasValueName (0x02) set, and names are identifiers (2 byte counts)
            MorphWriter writer = Parameters.Encode(new object[] { new Named { N = 1 } }, new InstanceFactories());
            byte[] all = writer.ToArray();
            //  Struct header:  IsStruct | HasTypeName = 0x21, "Named", StructElemCount = 1
            //  Member:  IsSimple | HasValueName = 0x02, "N", SimpleType 0x60, value
            byte[] expected = new byte[]
            {
                0x00, 0x00, 0x00, 0x01,                                             //  ParamCount
                0x21, 0x00, 0x05, (byte)'N', (byte)'a', (byte)'m', (byte)'e', (byte)'d',
                0x00, 0x00, 0x00, 0x01,                                             //  StructElemCount
                0x02, 0x00, 0x01, (byte)'N',
                0x60, 0x00, 0x00, 0x00, 0x01,
            };
            Assert.That(all, Is.EqualTo(expected));
        }

        public struct Named
        {
            public int N;
        }
    }
}
