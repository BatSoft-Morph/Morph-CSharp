using Morph.Core;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace Morph.Params
{
    internal class EncoderStruct : Encoder
    {
        public EncoderStruct(Encoders registry, Type type)
        {
            this.registry = registry;
            structType = type;
        }

        private readonly Encoders registry;
        private readonly Type structType;
        private FieldInfo[] fields;
        private readonly List<Encoder> encoderSets = new List<Encoder>();

        public override void EncodeType(MorphWriter writer, string typeName, string valueName)
        {
            //  ValueType
            WriteValueType(writer, ValueType.IsStruct, typeName, valueName);
            //  StructElemCount
            fields = structType.GetFields();
            fields = Array.FindAll(fields, field => field.IsPublic && !field.IsStatic && !field.IsLiteral);
            writer.WriteInt32(fields.Length);
            //  Encoder sets
            foreach (var field in fields)
                encoderSets.Add(registry.FindEncoder(field.FieldType));
        }

        public override void EncodeValue(MorphWriter writer, object value)
        {
            for (int i = 0; i < fields.Length; i++)
            {
                //  This is wordy for debugging and analysis purposes
                Encoder encoderSet = encoderSets[i];
                FieldInfo field = fields[i];
                object elemValue = field.GetValue(value);
                encoderSet.EncodeValue(writer, elemValue);
            }
        }
    }
}