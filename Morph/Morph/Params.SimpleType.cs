using Morph.Core;
using System;
using System.Collections.Generic;
using System.Text;

namespace Morph.Params
{
    public delegate void EncodeSimpleType(MorphWriter writer);
    public delegate void EncodeSimpleValue(MorphWriter writer, object value);

    static public class SimpleType
    {
        #region Constants

        public const byte IsInteger = 0x0;
        public const byte IsCharacter = 0x01;
        public const byte IsFloat = 0x02;
        public const byte IsString = 0x03;
        public const byte IsEnumOrd = 0x04;
        public const byte IsEnumStr = 0x05;
        public const byte IsWhen = 0x07;
        public const byte IsCurrency = 0x08;
        public const byte IsBool = 0x0C;
        //public const byte Is = 0x0;

        //  Sizes
        public const byte MaskValueSize = 0xC0;
        public const byte Size8 = 0x00;
        public const byte Size16 = 0x40;
        public const byte Size32 = 0x80;
        public const byte Size64 = 0xC0;

        //  IsCharacter
        public const byte MaskCharEncoding = MaskValueSize;
        public const byte IsUTF8 = Size8;
        public const byte IsUTF16 = Size16;
        public const byte IsUTF32 = Size32;
        public const byte IsASCII = Size64;

        //  Enumeration
        public const byte IsEnumSet = 0x10;

        public const byte MaskBool = 0x0C;
        public const byte IsFalse = MaskBool;
        public const byte IsTrue = MaskBool | 0x80;

        //  Specific types
        public const byte IsByte = IsInteger | Size8;

        #endregion

        #region Encoding

        private readonly struct Encoders
        {
            public readonly EncodeSimpleType typeEncoder;
            public readonly EncodeSimpleValue valueEncoder;

            public Encoders(EncodeSimpleType simpleTypeEncoder, EncodeSimpleValue simpleValueEncoder)
            {
                this.typeEncoder = simpleTypeEncoder;
                this.valueEncoder = simpleValueEncoder;
            }
        }

        private static readonly Dictionary<Type, Encoders> simpleEncoders = new Dictionary<Type, Encoders>()
        {
            { typeof(bool), new Encoders(
                delegate (MorphWriter writer) { writer.WriteInt8(IsFalse); },
                delegate (MorphWriter writer, object value) { writer.WriteInt8((bool)value ? IsTrue : IsFalse); })},

            { typeof(byte), new Encoders(
                delegate (MorphWriter writer) { writer.WriteInt8(IsInteger | Size8); },
                delegate (MorphWriter writer, object value) { writer.WriteInt8((byte)value); })},

            { typeof(Int16), new Encoders(
                delegate (MorphWriter writer) { writer.WriteInt8(IsInteger | Size16); },
                delegate (MorphWriter writer, object value) { writer.WriteInt16((Int16)value); })},

            { typeof(Int32), new Encoders(
                delegate (MorphWriter writer) { writer.WriteInt8(IsInteger | Size32); },
                delegate (MorphWriter writer, object value) { writer.WriteInt32((Int32)value); })},

            { typeof(Int64), new Encoders(
                delegate (MorphWriter writer) { writer.WriteInt8(IsInteger | Size64); },
                delegate (MorphWriter writer, object value) { writer.WriteInt64((Int64)value); })},

            { typeof(string), new Encoders(
                delegate (MorphWriter writer) { writer.WriteInt8(IsString); },
                delegate (MorphWriter writer, object value) { writer.WriteString((string)value); })},

            { typeof(DateTime), new Encoders(
                delegate (MorphWriter writer) { writer.WriteInt8(IsWhen); },
                delegate (MorphWriter writer, object value)
                {
                    DateTime when = (DateTime)value;
                    string timeZone = when.Kind == DateTimeKind.Utc ? "Z" : null;
                    byte[] bytes = Encoding.UTF8.GetBytes(string.Concat(when.ToString("yyyy’-‘MM’-‘dd’T’HH’:’mm’:’ss"), timeZone));
                    writer.WriteInt8((byte)bytes.Length);
                    writer.WriteBytes(bytes);
                })},

            //{ typeof(qwerty), new Encoders(
            //    qwerty,
            //    qwertyu)},
            //
        };

        static public bool CanEncode(Type type, out EncodeSimpleType typeEncoder, out EncodeSimpleValue valueEncoder)
        {
            if (simpleEncoders.TryGetValue(type, out Encoders encoders))
            {
                typeEncoder = encoders.typeEncoder;
                valueEncoder = encoders.valueEncoder;
                return true;
            }
            else
            {
                typeEncoder = null;
                valueEncoder = null;
                return false;
            }
        }

        #endregion
    }

}