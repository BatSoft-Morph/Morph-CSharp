using Morph.Core;

namespace Morph.Params
{
    public abstract class Decoder
    {
        public abstract object DecodeValue(MorphReader reader);
    }

    public class Decoders
    {
        public Decoders()
        {
        }

        public Decoder Find(MorphReader reader)
        {
            //  Read ValueType
            byte valueType = reader.ReadInt8();

            string ReadString(byte flag) => (valueType & flag) != 0 ? reader.ReadString() : null;
            bool HasFlag(byte flag) => (valueType & flag) != 0;

            string typeName = ReadString(ValueType.HasTypeName);
            string valueName = ReadString(ValueType.HasValueName);
            if (HasFlag(ValueType.IsReference))
            {
                if (HasFlag(ValueType.IsNull))
                    ;
            }
            else
                switch (valueType & ValueType.ValueMask)
                {
                    case ValueType.IsSimpleType:
                        // Handle simple type decoding
                        break;
                    case ValueType.IsArray:
                        // Handle array decoding
                        break;
                    case ValueType.IsStruct:
                        // Handle struct decoding
                        break;
                    case ValueType.IsCustom:
                        // Handle custom type decoding
                        break;
                }
        }
    }
}