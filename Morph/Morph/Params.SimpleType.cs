using Morph.Core;
using Morph.Lib;
using System;
using System.CodeDom;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Morph.Params
{
    static public class SimpleType
    {
        #region Constants

        public const byte TypeMask = 0x0F;
        public const byte TypeInteger = 0x00;
        public const byte TypeChar = 0x01;
        public const byte TypeFloat = 0x02;
        public const byte TypeString = 0x03;
        public const byte TypeEnumOrd = 0x04;
        public const byte TypeEnumStr = 0x05;
        public const byte TypeWhenStr = 0x07;
        public const byte TypeCurrency = 0x0A;
        public const byte TypeBoolean = 0x0C;

        public const byte ValueSizeMask = 0x30;
        public const byte Size8Bit = 0x00;
        public const byte Size16Bit = 0x10;
        public const byte Size32Bit = 0x20;
        public const byte Size64Bit = 0x30;

        public const byte WhenDateTime = 0x30;

        public const byte IsSigned = 0x40;
        public const byte HasValue = 0x80;

        public const byte BoolFalse = 0x00;
        public const byte BoolTrue = 0x10;

        #endregion

        static SimpleType()
        {
            RegisterEncoders();
            RegisterDecoders();
        }

        static private byte SimpleTypeByte(byte typeNibble, byte valueSize, bool isSigned, bool hasValue)
            => (byte)(typeNibble | valueSize | (byte)(isSigned ? IsSigned : 0) | (byte)(hasValue ? HasValue : 0));

        #region Type conversion

        [StructLayout(LayoutKind.Explicit)]
        private struct ConverterUnion<T1, T2>
        {
            [FieldOffset(0)] public T1 value1;
            [FieldOffset(0)] public T2 value2;
        }

        private static T2 ConvertNumber<T1, T2>(object value)
            => new ConverterUnion<T1, T2> { value1 = (T1)value }.value2;

        #endregion

        #region Encoding

        delegate void Encoder(MorphWriter writer, object value, bool encodeType, bool encodeValue);
        delegate void ValueEncoder(MorphWriter writer, object value);

        static private readonly Dictionary<Type, Encoder> encoders = new Dictionary<Type, Encoder>();

        static private void RegisterEncoders()
        {
            void AddEncoder(Type type, byte typeNibble, byte valueSize, bool isSigned, ValueEncoder valueEncoder) =>
                encoders.Add(type, (writer, value, encodeType, encodeValue) =>
                {
                    if (encodeType) writer.WriteInt8(SimpleTypeByte(typeNibble, valueSize, isSigned, encodeValue));
                    if (encodeValue) valueEncoder(writer, value);
                });

            //  Boolean
            encoders.Add(typeof(bool), (writer, value, encodeType, encodeValue) =>
            {
                writer.WriteInt8(TypeBoolean | (encodeValue ? HasValue : 0) | ((bool)value ? BoolTrue : BoolFalse));
            });
            //  Unsigned integers
            AddEncoder(typeof(byte), TypeInteger, Size8Bit, false, (writer, value) => writer.WriteInt8((byte)value));
            AddEncoder(typeof(UInt16), TypeInteger, Size16Bit, false, (writer, value) => writer.WriteInt16(ConvertNumber<UInt16, Int16>(value)));
            AddEncoder(typeof(UInt32), TypeInteger, Size32Bit, false, (writer, value) => writer.WriteInt32(ConvertNumber<UInt32, Int32>(value)));
            AddEncoder(typeof(UInt64), TypeInteger, Size64Bit, false, (writer, value) => writer.WriteInt64(ConvertNumber<UInt64, Int64>(value)));
            //  Signed integers
            AddEncoder(typeof(sbyte), TypeInteger, Size8Bit, true, (writer, value) => writer.WriteInt8((sbyte)value));
            AddEncoder(typeof(Int16), TypeInteger, Size16Bit, true, (writer, value) => writer.WriteInt16((Int16)value));
            AddEncoder(typeof(Int32), TypeInteger, Size32Bit, true, (writer, value) => writer.WriteInt32((Int32)value));
            AddEncoder(typeof(Int64), TypeInteger, Size64Bit, true, (writer, value) => writer.WriteInt64((Int64)value));
            //  Floating point
            AddEncoder(typeof(float), TypeFloat, Size8Bit, true, (writer, value) => writer.WriteInt32(ConvertNumber<float, Int32>(value)));
            AddEncoder(typeof(double), TypeFloat, Size16Bit, true, (writer, value) => writer.WriteInt64(ConvertNumber<double, Int64>(value)));

            //  Char
            //AddEncoder(typeof(char), TypeChar, Size16Bit, false, (value) => BitConverter.GetBytes((char)value));
            //  String
            AddEncoder(typeof(string), TypeString, Size32Bit, false, (writer, value) => writer.WriteString((string)value));
            //  Enumerations (Ordinal)

            //  Enumerations (String)

            //  When (String)
            AddEncoder(typeof(DateTime), TypeWhenStr, WhenDateTime, false, (writer, value) =>
            {
                string whenStr = Conversion.DateTimeToStr((DateTime)value);
                byte[] buffer = Encoding.UTF8.GetBytes(whenStr);
                writer.WriteBytes(buffer.Prepend((byte)buffer.Length).ToArray());
            });
        }

        static public bool Encode(MorphWriter writer, object value, bool encodeType, bool encodeValue)
        {
            Type type = value.GetType();
            if (encoders.TryGetValue(type, out Encoder encoder))
            {
                encoder(writer, value, encodeType, encodeValue);
                return true;
            }
            else
                return false;
            //throw new EMorph(0, $"Unsupported simple type: {type.FullName}");
        }

        #endregion

        #region Decoding

        delegate object Decoder(MorphReader reader, bool hasValue);
        delegate object ConverterFromBytes(MorphReader reader);

        static private readonly Dictionary<byte, Decoder> decoders = new Dictionary<byte, Decoder>();

        static private void RegisterDecoders()
        {
            void AddDecoder(Type type, byte typeNibble, byte valueSize, bool isSigned, ConverterFromBytes converter) =>
                decoders.Add(SimpleTypeByte(typeNibble, valueSize, isSigned, false), (reader, hasValue) =>
                {
                    if (hasValue)
                        return converter(reader);
                    else
                        return type;
                });

            //  Boolean
            decoders.Add(TypeBoolean | BoolFalse, (reader, hasValue) => hasValue ? (object)false : typeof(bool));
            decoders.Add(TypeBoolean | BoolTrue, (reader, hasValue) => hasValue ? (object)true : typeof(bool));
            //  Unsigned integers
            AddDecoder(typeof(byte), TypeInteger, Size8Bit, false, (reader) => reader.ReadInt8());
            AddDecoder(typeof(UInt16), TypeInteger, Size16Bit, false, (reader) => ConvertNumber<Int16, UInt16>(reader.ReadInt16()));
            AddDecoder(typeof(UInt32), TypeInteger, Size32Bit, false, (reader) => ConvertNumber<Int32, UInt32>(reader.ReadInt32()));
            AddDecoder(typeof(UInt64), TypeInteger, Size64Bit, false, (reader) => ConvertNumber<Int64, UInt64>(reader.ReadInt64()));
            //  Signed integers
            AddDecoder(typeof(sbyte), TypeInteger, Size8Bit, true, (reader) => reader.ReadInt8());
            AddDecoder(typeof(Int16), TypeInteger, Size16Bit, true, (reader) => reader.ReadInt16());
            AddDecoder(typeof(Int32), TypeInteger, Size32Bit, true, (reader) => reader.ReadInt32());
            AddDecoder(typeof(Int64), TypeInteger, Size64Bit, true, (reader) => reader.ReadInt64());
            //  Floating point
            AddDecoder(typeof(float), TypeFloat, Size8Bit, true, (reader) => ConvertNumber<Int32, float>(reader.ReadInt32()));
            AddDecoder(typeof(double), TypeFloat, Size16Bit, true, (reader) => ConvertNumber<Int64, double>(reader.ReadInt64()));

            //  Char

            //  String
            AddDecoder(typeof(string), TypeString, Size32Bit, false, (reader) => reader.ReadString());
            //  Enumerations (Ordinal)

            //  Enumerations (String)

            //  When (String)
            AddDecoder(typeof(DateTime), TypeWhenStr, WhenDateTime, false, (reader) =>
            {
                int length = reader.ReadInt8();
                byte[] buffer = reader.ReadBytes(length);
                string whenStr = Encoding.UTF8.GetString(buffer);
                return Conversion.StrToDateTime(whenStr);
            });
        }

        static public object Decode(MorphReader reader)
        {
            byte simpleType = reader.ReadInt8();
            bool hasValue = (simpleType & HasValue) != 0;
            simpleType = (byte)(simpleType & ~HasValue);

            if (decoders.TryGetValue(simpleType, out Decoder decoder))
                return decoder(reader, hasValue);
            throw new EMorph(0, $"Unsupported simple type: {simpleType}");
        }

        #endregion
    }
}