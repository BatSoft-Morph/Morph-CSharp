using Morph.Core;

namespace Morph.Params
{
    public class SimpleTypeNew
    {
        public const byte IsBool = 0x0C;
    }

    internal class EncoderNumeration : Encoder
    {
        public delegate void ValueEncoder(MorphWriter writer, object value);
        public delegate object ValueDecoder(MorphReader reader);

        internal EncoderNumeration(byte typeNibble, byte valueSize, bool isSigned, ValueEncoder valueEncoder)
        {
            this.typeNibble = typeNibble;
            this.valueSize = valueSize;
            this.isSigned = isSigned;
            this.valueEncoder = valueEncoder;
        }

        private readonly byte typeNibble;
        private readonly byte valueSize;
        private readonly bool isSigned;
        private readonly ValueEncoder valueEncoder;

        protected void WriteValueType(MorphWriter writer, string typeName, string valueName)
            => WriteValueType(writer, ValueType.IsSimpleType, typeName, valueName);

        protected void WriteSimpleType(MorphWriter writer, byte typeNibble, byte valueSize, bool isSigned)
            => writer.WriteInt8((byte)(typeNibble | valueSize | (byte)(isSigned ? SimpleType.IsSigned : 0)));

        public override void EncodeType(MorphWriter writer, string typeName, string valueName)
        {
            WriteValueType(writer, typeName, valueName);
            WriteSimpleType(writer, typeNibble, valueSize, isSigned);
        }

        public override void EncodeValue(MorphWriter writer, object value)
            => valueEncoder(writer, value);
    }


    //internal abstract class EncoderGeneric<T> : EncoderSimple
    //{
    //    internal EncoderGeneric(byte typeNibble, bool isSigned)
    //    {
    //        this.typeNibble = typeNibble;
    //        Type type = typeof(T);
    //        switch (Marshal.SizeOf(type))
    //        {
    //            case 1: valueSize = SimpleType.Size8Bit; break;
    //            case 2: valueSize = SimpleType.Size16Bit; break;
    //            case 4: valueSize = SimpleType.Size32Bit; break;
    //            case 8: valueSize = SimpleType.Size64Bit; break;
    //            default: EMorph.Throw(EMorph.Any, $"Unsupported size for type {type.FullName}", null); break;
    //        }
    //        this.isSigned = isSigned;
    //    }

    //    private readonly byte typeNibble;
    //    private readonly byte valueSize;
    //    private readonly bool isSigned;

    //    public override void EncodeType(MorphWriter writer, string typeName, string valueName)
    //    {
    //        WriteValueType(writer, typeName, valueName);
    //        WriteSimpleType(writer, typeNibble, valueSize, isSigned);
    //    }
    //}

    internal class EncoderBool : EncoderNumeration
    {
        public EncoderBool() : base(SimpleTypeNew.IsBool, SimpleType.Size8Bit, false, (writer, value) => writer.WriteInt8((byte)((bool)value ? 0xFF : 0)))
        { }
    }

    internal class EncoderByte : EncoderNumeration
    {
        public EncoderByte() : base(SimpleType.TypeInteger, SimpleType.Size8Bit, false, (writer, value) => writer.WriteInt8((byte)value))
        { }
    }
}