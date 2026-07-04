using Morph.Core;
using Morph.Lib;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using System.Xml;

namespace Morph.Params
{
    /// <summary>
    /// <para>The SimpleType byte and its value payload.</para>
    /// <para>Layout per "Morph Protocol.xlsx" tab "SimpleType".</para>
    /// <para>Whether a value follows the SimpleType byte depends on context:
    /// encoding a value, the value follows immediately;
    /// encoding a type (an array's element type), nothing follows.</para>
    /// </summary>
    public static class SimpleType
    {
        #region Byte layout

        //  Bits 0-3:  the type
        public const byte TypeMask = 0x0F;
        public const byte TypeInteger = 0x00;
        public const byte TypeChar = 0x01;
        public const byte TypeFloat = 0x02;
        public const byte TypeString = 0x03;
        public const byte TypeEnumOrdinal = 0x04;
        public const byte TypeEnumString = 0x05;
        /// <summary>"Under construction.  Do not use."</summary>
        public const byte TypeWhenNumeric = 0x06;
        public const byte TypeWhenString = 0x07;
        public const byte TypeCurrency = 0x0A;

        //  Bits 4-5:  size exponent z, where ValueSize = 2^z bytes
        public const byte SizeMask = 0x30;
        public const int SizeShift = 4;
        public const byte Size1Byte = 0x00;
        public const byte Size2Bytes = 0x10;
        public const byte Size4Bytes = 0x20;
        public const byte Size8Bytes = 0x30;

        //  Float reinterprets z:  ValueSize = 2^(z+2) bytes
        public const byte FloatSize4Bytes = 0x00;
        public const byte FloatSize8Bytes = 0x10;

        //  Char reinterprets z as the character encoding
        public const byte CharUTF8 = 0x00;
        public const byte CharUTF16 = 0x10;
        public const byte CharUTF32 = 0x20;
        public const byte CharASCII = 0x30;

        //  When (string) reinterprets z as the kind of time value
        public const byte WhenDuration = 0x00;
        public const byte WhenTime = 0x10;
        public const byte WhenDate = 0x20;
        public const byte WhenDateTime = 0x30;

        //  Bit 6:  meaning depends on the type
        public const byte IsSigned = 0x40;      //  Integer
        public const byte IsEnumSet = 0x40;     //  Enums

        //  Bit 7
        public const byte IsBool = 0x80;        //  Enum (ordinal)

        /// <summary>"The full byte value for boolean is 0x84."  A boolean carries no enum type name.</summary>
        public const byte Boolean = IsBool | TypeEnumOrdinal;

        #endregion

        #region Float bit conversion (netstandard2.0 has no Int32BitsToSingle)

        [StructLayout(LayoutKind.Explicit)]
        private struct SingleUnion
        {
            [FieldOffset(0)] public float Single;
            [FieldOffset(0)] public int Int32;
        }

        #endregion

        #region Encoding

        public delegate void ValueWriter(MorphWriter writer, object value);

        private struct Codec
        {
            public byte SimpleTypeByte;
            public ValueWriter WriteValue;
        }

        private static readonly Dictionary<Type, Codec> s_codecs = CreateCodecs();

        private static Dictionary<Type, Codec> CreateCodecs()
        {
            Dictionary<Type, Codec> codecs = new Dictionary<Type, Codec>();
            void Add(Type type, byte simpleTypeByte, ValueWriter writeValue)
                => codecs.Add(type, new Codec { SimpleTypeByte = simpleTypeByte, WriteValue = writeValue });
            //  Boolean
            Add(typeof(bool), Boolean,
                (writer, value) => writer.WriteInt8((bool)value ? 0xFF : 0x00));
            //  Unsigned integers
            Add(typeof(byte), TypeInteger | Size1Byte,
                (writer, value) => writer.WriteInt8((byte)value));
            Add(typeof(ushort), TypeInteger | Size2Bytes,
                (writer, value) => writer.WriteInt16((ushort)value));
            Add(typeof(uint), TypeInteger | Size4Bytes,
                (writer, value) => writer.WriteInt32((int)(uint)value));
            Add(typeof(ulong), TypeInteger | Size8Bytes,
                (writer, value) => writer.WriteInt64((long)(ulong)value));
            //  Signed integers
            Add(typeof(sbyte), TypeInteger | Size1Byte | IsSigned,
                (writer, value) => writer.WriteInt8((sbyte)value));
            Add(typeof(short), TypeInteger | Size2Bytes | IsSigned,
                (writer, value) => writer.WriteInt16((short)value));
            Add(typeof(int), TypeInteger | Size4Bytes | IsSigned,
                (writer, value) => writer.WriteInt32((int)value));
            Add(typeof(long), TypeInteger | Size8Bytes | IsSigned,
                (writer, value) => writer.WriteInt64((long)value));
            //  Floating point
            Add(typeof(float), TypeFloat | FloatSize4Bytes,
                (writer, value) => writer.WriteInt32(new SingleUnion { Single = (float)value }.Int32));
            Add(typeof(double), TypeFloat | FloatSize8Bytes,
                (writer, value) => writer.WriteInt64(BitConverter.DoubleToInt64Bits((double)value)));
            //  Char (a C# char is a UTF-16 code unit)
            Add(typeof(char), TypeChar | CharUTF16,
                (writer, value) => writer.WriteInt16((char)value));
            //  String:  ByteCount (4 bytes) + UTF-8
            Add(typeof(string), TypeString,
                (writer, value) => writer.WriteString((string)value));
            //  Currency:  8 bytes, fixed point scaled by 10 000
            Add(typeof(decimal), TypeCurrency,
                (writer, value) => writer.WriteInt64((long)Math.Round((decimal)value * 10000m)));
            //  Date/time (string form)
            Add(typeof(DateTime), TypeWhenString | WhenDateTime,
                (writer, value) => WriteWhenString(writer, Conversion.DateTimeToStr((DateTime)value)));
            Add(typeof(TimeSpan), TypeWhenString | WhenDuration,
                (writer, value) => WriteWhenString(writer, XmlConvert.ToString((TimeSpan)value)));
            return codecs;
        }

        static public bool TryGetCodec(Type type, out byte simpleTypeByte, out ValueWriter writeValue)
        {
            if (s_codecs.TryGetValue(type, out Codec codec))
            {
                simpleTypeByte = codec.SimpleTypeByte;
                writeValue = codec.WriteValue;
                return true;
            }
            simpleTypeByte = 0;
            writeValue = null;
            return false;
        }

        //  When (string) values carry a 1 byte StringLength
        static private void WriteWhenString(MorphWriter writer, string when)
        {
            byte[] buffer = Encoding.UTF8.GetBytes(when);
            writer.WriteInt8((byte)buffer.Length);
            writer.WriteBytes(buffer);
        }

        #endregion

        #region Encoding enums

        static public byte EnumSimpleTypeByte(Type enumType)
        {
            byte size;
            switch (Marshal.SizeOf(Enum.GetUnderlyingType(enumType)))
            {
                case 1: size = Size1Byte; break;
                case 2: size = Size2Bytes; break;
                case 4: size = Size4Bytes; break;
                case 8: size = Size8Bytes; break;
                default: throw new EMorphImplementation();
            }
            byte result = (byte)(TypeEnumOrdinal | size);
            if (enumType.IsDefined(typeof(FlagsAttribute), false))
                result |= IsEnumSet;
            return result;
        }

        /// <summary>An enum value's payload is its type name (a string:  4 byte count) followed by its ordinal.</summary>
        static public void WriteEnumValue(MorphWriter writer, object value)
        {
            Type enumType = value.GetType();
            writer.WriteString(enumType.Name);
            WriteInteger(writer, Convert.ToInt64(value, CultureInfo.InvariantCulture), EnumSimpleTypeByte(enumType));
        }

        static private void WriteInteger(MorphWriter writer, long value, byte simpleTypeByte)
        {
            switch (simpleTypeByte & SizeMask)
            {
                case Size1Byte: writer.WriteInt8((int)value); break;
                case Size2Bytes: writer.WriteInt16((int)value); break;
                case Size4Bytes: writer.WriteInt32((int)value); break;
                case Size8Bytes: writer.WriteInt64(value); break;
            }
        }

        #endregion

        #region Decoding

        /// <summary>
        /// The CLR type that ReadValue produces for a SimpleType byte,
        /// or null when it depends on the value (enums with type names).
        /// </summary>
        static public Type ToClrType(byte simpleTypeByte)
        {
            int z = (simpleTypeByte & SizeMask) >> SizeShift;
            switch (simpleTypeByte & TypeMask)
            {
                case TypeInteger:
                    if ((simpleTypeByte & IsSigned) != 0)
                        switch (z)
                        {
                            case 0: return typeof(sbyte);
                            case 1: return typeof(short);
                            case 2: return typeof(int);
                            default: return typeof(long);
                        }
                    else
                        switch (z)
                        {
                            case 0: return typeof(byte);
                            case 1: return typeof(ushort);
                            case 2: return typeof(uint);
                            default: return typeof(ulong);
                        }
                case TypeChar:
                    return typeof(char);
                case TypeFloat:
                    switch (z)
                    {
                        case 0: return typeof(float);
                        case 1: return typeof(double);
                        default: return null;
                    }
                case TypeString:
                    return typeof(string);
                case TypeEnumOrdinal:
                    return (simpleTypeByte & IsBool) != 0 ? typeof(bool) : null;
                case TypeWhenString:
                    switch (simpleTypeByte & SizeMask)
                    {
                        case WhenDuration:
                        case WhenTime:
                            return typeof(TimeSpan);
                        default:
                            return typeof(DateTime);
                    }
                case TypeCurrency:
                    return typeof(decimal);
                default:
                    return null;
            }
        }

        static public object ReadValue(MorphReader reader, InstanceFactories instanceFactories)
            => ReadValue(reader, reader.ReadInt8(), instanceFactories);

        static public object ReadValue(MorphReader reader, byte simpleTypeByte, InstanceFactories instanceFactories)
        {
            int z = (simpleTypeByte & SizeMask) >> SizeShift;
            switch (simpleTypeByte & TypeMask)
            {
                case TypeInteger:
                    return ReadInteger(reader, z, (simpleTypeByte & IsSigned) != 0);
                case TypeChar:
                    return ReadChar(reader, simpleTypeByte & SizeMask);
                case TypeFloat:
                    return ReadFloat(reader, z);
                case TypeString:
                    return reader.ReadString();
                case TypeEnumOrdinal:
                    return ReadEnumOrdinal(reader, simpleTypeByte, z, instanceFactories);
                case TypeEnumString:
                    return ReadEnumString(reader, instanceFactories);
                case TypeWhenNumeric:
                    throw new EMorph("SimpleType When (numeric) is under construction in the Morph definition.  Do not use.");
                case TypeWhenString:
                    return ReadWhenString(reader, simpleTypeByte & SizeMask);
                case TypeCurrency:
                    return reader.ReadInt64() / 10000m;
                default:
                    throw new EMorph("Unknown SimpleType: 0x" + simpleTypeByte.ToString("X2"));
            }
        }

        static private object ReadInteger(MorphReader reader, int z, bool isSigned)
        {
            if (isSigned)
                switch (z)
                {
                    case 0: return (sbyte)reader.ReadInt8();
                    case 1: return reader.ReadInt16();
                    case 2: return reader.ReadInt32();
                    default: return reader.ReadInt64();
                }
            else
                switch (z)
                {
                    case 0: return reader.ReadInt8();
                    case 1: return (ushort)reader.ReadInt16();
                    case 2: return (uint)reader.ReadInt32();
                    default: return (ulong)reader.ReadInt64();
                }
        }

        static private object ReadChar(MorphReader reader, int charEncoding)
        {
            switch (charEncoding)
            {
                case CharUTF8:
                    {
                        byte leading = reader.ReadInt8();
                        int byteCount =
                            leading < 0x80 ? 1 :
                            (leading & 0xE0) == 0xC0 ? 2 :
                            (leading & 0xF0) == 0xE0 ? 3 : 4;
                        byte[] bytes = new byte[byteCount];
                        bytes[0] = leading;
                        for (int i = 1; i < byteCount; i++)
                            bytes[i] = reader.ReadInt8();
                        string chars = Encoding.UTF8.GetString(bytes);
                        if (chars.Length != 1)
                            throw new EMorph("UTF-8 character does not fit in a single UTF-16 char");
                        return chars[0];
                    }
                case CharUTF16:
                    return (char)reader.ReadInt16();
                case CharUTF32:
                    {
                        string chars = char.ConvertFromUtf32(reader.ReadInt32());
                        if (chars.Length != 1)
                            throw new EMorph("UTF-32 character does not fit in a single UTF-16 char");
                        return chars[0];
                    }
                default:    //  CharASCII
                    return (char)reader.ReadInt8();
            }
        }

        static private object ReadFloat(MorphReader reader, int z)
        {
            switch (z)
            {
                case 0: return new SingleUnion { Int32 = reader.ReadInt32() }.Single;
                case 1: return BitConverter.Int64BitsToDouble(reader.ReadInt64());
                default: throw new EMorph("Floats larger than 8 bytes are not supported on this implementation");
            }
        }

        static private object ReadEnumOrdinal(MorphReader reader, byte simpleTypeByte, int z, InstanceFactories instanceFactories)
        {
            //  A boolean is an enum with the IsBool flag, and carries no enum type name
            if ((simpleTypeByte & IsBool) != 0)
                return Convert.ToUInt64(ReadInteger(reader, z, false), CultureInfo.InvariantCulture) != 0;
            //  Enum type name, then the ordinal
            string enumTypeName = reader.ReadString();
            object ordinal = ReadInteger(reader, z, false);
            Type enumType = instanceFactories?.FindEnumType(enumTypeName);
            if (enumType != null)
                return Enum.ToObject(enumType, Convert.ToInt64(ordinal, CultureInfo.InvariantCulture));
            //  Unknown enum type:  degrade to the ordinal
            return ordinal;
        }

        static private object ReadEnumString(MorphReader reader, InstanceFactories instanceFactories)
        {
            string enumTypeName = reader.ReadString();
            //  "Values are comma delimited, without spaces.  Ending comma is ignored."
            string names = reader.ReadString().TrimEnd(',');
            Type enumType = instanceFactories?.FindEnumType(enumTypeName);
            if (enumType != null)
                return Enum.Parse(enumType, names, false);
            //  Unknown enum type:  degrade to the names
            return names;
        }

        static private object ReadWhenString(MorphReader reader, int whenKind)
        {
            string when = Encoding.UTF8.GetString(reader.ReadBytes(reader.ReadInt8()));
            switch (whenKind)
            {
                case WhenDuration: return XmlConvert.ToTimeSpan(when);
                case WhenTime: return TimeSpan.Parse(when, CultureInfo.InvariantCulture);
                case WhenDate: return DateTime.ParseExact(when, "yyyy-MM-dd", CultureInfo.InvariantCulture);
                default: return Conversion.StrToDateTime(when);     //  WhenDateTime
            }
        }

        #endregion
    }
}
