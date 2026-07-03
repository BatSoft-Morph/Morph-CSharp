using Morph.Core;
using System;

namespace Morph.Params
{
    internal class EncoderArray : Encoder
    {
        public EncoderArray(Encoders registry, Type type)
        {
            this.registry = registry;
            arrayType = type;
        }

        private readonly Encoders registry;
        private readonly Type arrayType;
        private bool useArrayElemType;
        private Encoder elemEncoder;

        public override void EncodeType(MorphWriter writer, string typeName, string valueName)
        {
            Type elementType = arrayType.GetElementType();
            useArrayElemType = elementType.IsValueType || elementType == typeof(string);
            //  ValueType
            byte valueType = ValueType.IsArray;
            valueType |= useArrayElemType ? ValueType.ArrayElemType : (byte)0;
            WriteValueType(writer, valueType, typeName, valueName);
            //  ArrayElemType
            elemEncoder = registry.FindEncoder(elementType);
            if (useArrayElemType)
                elemEncoder.EncodeType(writer, elementType.FullName, null);
        }

        public override void EncodeValue(MorphWriter writer, object value)
        {
            Array array = (Array)value;
            //  ArrayElemCount
            writer.WriteInt32(array.Length);
            //  Write values
            if (useArrayElemType)
                for (int i = 0; i < array.Length; i++)
                    elemEncoder.EncodeValue(writer, array.GetValue(i));
            else
                for (int i = 0; i < array.Length; i++)
                {
                    object elemValue = array.GetValue(i);
                    elemEncoder = registry.FindEncoder(elemValue);
                    elemEncoder.EncodeType(writer, null, null);
                    elemEncoder.EncodeValue(writer, elemValue);
                }
        }
    }
}