using Morph.Core;
using Morph.Lib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace Morph.Params
{
    public class ValueType
    {
        //  Basic       
        public const byte HasTypeName = 0x01;
        public const byte HasValueName = 0x02;
        public const byte IsReference = 0x04;
        public const byte IsNull = 0x08;

        //  By value
        public const byte ValueMask = 0x30;
        public const byte IsSimpleType = 0x00;
        public const byte IsArray = 0x10;
        public const byte IsStruct = 0x20;
        public const byte IsCustom = 0x30;

        public const byte ArrayElemType = 0x40;

        //  By reference
        public const byte IsServlet = 0x10;
        //  Servlet
        public const byte HasDevicePath = 0x20;
        public const byte HasArrayIndex = 0x40;
    }

    public delegate bool TypeRecogniser(Type type);
    public delegate Encoder EncoderGenerator(Type type);

    public abstract class Encoder
    {
        static protected void WriteValueType(MorphWriter writer, byte valueType, string typeName, string valueName)
        {
            if (valueName != null) valueType |= ValueType.HasValueName;
            if (typeName != null) valueType |= ValueType.HasTypeName;
            writer.WriteInt8(valueType);
            if (valueName != null) writer.WriteIdentifier(valueName);
            if (typeName != null) writer.WriteIdentifier(typeName);
        }

        public abstract void EncodeType(MorphWriter writer, string typeName, string valueName);

        public abstract void EncodeValue(MorphWriter writer, object value);
    }

    public class EncoderSetNull : Encoder
    {
        public override void EncodeType(MorphWriter writer, string typeName, string valueName)
            => WriteValueType(writer, ValueType.IsNull, typeName, valueName);

        public override void EncodeValue(MorphWriter writer, object value)
            => writer.WriteInt8(0);
    }

    public class Encoders
    {
        public Encoders()
        {
            //  Boolean
            simpleTypes.Add(typeof(bool), new EncoderNumeration(SimpleTypeNew.IsBool, SimpleType.Size8Bit, false, (writer, value) => writer.WriteInt8((byte)((bool)value ? 0xFF : 0))));
            //  Unsigned integer types
            simpleTypes.Add(typeof(byte), new EncoderNumeration(SimpleType.TypeInteger, SimpleType.Size8Bit, false, (writer, value) => writer.WriteInt8((byte)value)));
            simpleTypes.Add(typeof(ushort), new EncoderNumeration(SimpleType.TypeInteger, SimpleType.Size16Bit, false, (writer, value) => writer.WriteInt16((short)value)));
            simpleTypes.Add(typeof(uint), new EncoderNumeration(SimpleType.TypeInteger, SimpleType.Size32Bit, false, (writer, value) => writer.WriteInt32((int)value)));
            simpleTypes.Add(typeof(ulong), new EncoderNumeration(SimpleType.TypeInteger, SimpleType.Size64Bit, false, (writer, value) => writer.WriteInt64((long)value)));
            //  Signed integer types
            simpleTypes.Add(typeof(sbyte), new EncoderNumeration(SimpleType.TypeInteger, SimpleType.Size8Bit, true, (writer, value) => writer.WriteInt8((sbyte)value)));
            simpleTypes.Add(typeof(short), new EncoderNumeration(SimpleType.TypeInteger, SimpleType.Size16Bit, true, (writer, value) => writer.WriteInt16((short)value)));
            simpleTypes.Add(typeof(int), new EncoderNumeration(SimpleType.TypeInteger, SimpleType.Size32Bit, true, (writer, value) => writer.WriteInt32((int)value)));
            simpleTypes.Add(typeof(long), new EncoderNumeration(SimpleType.TypeInteger, SimpleType.Size64Bit, true, (writer, value) => writer.WriteInt64((long)value)));
            //  Floating point types
            simpleTypes.Add(typeof(float), new EncoderNumeration(SimpleType.TypeFloat, SimpleType.Size32Bit, true, (writer, value) => writer.WriteInt32(ConvertNumber<float, Int32>(value))));
            simpleTypes.Add(typeof(double), new EncoderNumeration(SimpleType.TypeFloat, SimpleType.Size64Bit, true, (writer, value) => writer.WriteInt64(ConvertNumber<double, Int64>(value))));
            //  Char
            
            //  String type
            simpleTypes.Add(typeof(string), new EncoderNumeration(SimpleType.TypeString, SimpleType.Size32Bit, false, (writer, value) => writer.WriteString((string)value)));

            //  Enumerations (Ordinal)

            //  Enumerations (String)         

            //  When (String)
            simpleTypes.Add(typeof(DateTime), new EncoderNumeration(SimpleType.TypeWhenStr, SimpleType.WhenDateTime, false,
                (writer, value) =>
                {
                    string whenStr = Conversion.DateTimeToStr((DateTime)value);
                    byte[] buffer = Encoding.UTF8.GetBytes(whenStr);
                    writer.WriteBytes(buffer.Prepend((byte)buffer.Length).ToArray());
                }));
            //  Array types
            Register(type => type.IsArray, type => new EncoderArray(this, type));
            //  Struct types
            Register(type => type.IsValueType && !type.IsPrimitive && !type.IsEnum, type => new EncoderStruct(this, type));
        }

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

        private struct RecogniserPair
        {
            public TypeRecogniser recogniser;
            public EncoderGenerator encoderGenerator;
        }

        private readonly Dictionary<Type, Encoder> simpleTypes = new Dictionary<Type, Encoder>();
        private readonly List<RecogniserPair> complexTypes = new List<RecogniserPair>();

        public void Register(TypeRecogniser recogniser, EncoderGenerator encoderGenerator)
        {
            complexTypes.Add(new RecogniserPair { recogniser = recogniser, encoderGenerator = encoderGenerator });
        }

        public bool IsSimpleType(Type type)
            => SimpleType.GetEncoder(type, out _);  //XX    Use simpleTypes.ContainsKey(type);

        public Encoder FindEncoder(Type type)
        {
            //  Simple types
            if (simpleTypes.TryGetValue(type, out Encoder encoderSet))
                return encoderSet;
            //  Complex types
            //  Reference types
            foreach (var pair in complexTypes)
                if (pair.recogniser(type))
                    return pair.encoderGenerator(type);
            EMorph.Throw(EMorph.Any, $"No encoder set found for type {type.FullName}", null);
            return null;    //  Silence the compiler
        }

        public Encoder FindEncoder(object value)
        {
            if (value == null)
                return new EncoderSetNull();
            else
                return FindEncoder(value.GetType());
        }

        public Encoder FindDecoder(MorphReader reader)
        {
            byte valueType = reader.ReadInt8();


            if ((valueType & ValueType.IsNull) != 0)
                return new EncoderSetNull();
            else if ((valueType & ValueType.ValueMask) == ValueType.IsSimpleType)
            {


                return null;
            }
            else if ((valueType & ValueType.ValueMask) == ValueType.IsArray)
                return new EncoderArray(this, null);
            else if ((valueType & ValueType.ValueMask) == ValueType.IsStruct)
                return new EncoderStruct(this, null);
            else
                EMorph.Throw(EMorph.Any, $"No decoder set found for value type {valueType}", null);
            return null;    //  Silence the compiler
        }
    }
}