using Morph.Core;
using Morph.Endpoint;
using System;
using System.IO;
using System.Linq;
using System.Reflection;

namespace Morph.Params
{

    delegate bool EncodeValue(MorphWriter writer, byte valueType, object value);

    static public class ValueType
    {
        #region Constants

        public const byte Mask = 0x0E;
        public const byte IsNull = 0x00;
        public const byte IsSimpleType = 0x02;
        public const byte IsStruct = 0x04;
        public const byte IsArray = 0x06;
        public const byte IsServlet = 0x08;
        public const byte IsStream = 0x0A;
        public const byte IsException = 0x0C;
        public const byte IsCustomType = 0x0E;
        //  Basic
        public const byte HasValueName = 0x01;
        public const byte HasTypeName = 0x10;
        //  IsArray
        public const byte IsArrayElemType = 0x20;
        //  IsServlet
        public const byte HasArrayIndex = 0x40;
        public const byte HasDevicePath = 0x80;
        //  IsException
        public const byte HasExceptionCode = 0x40;
        public const byte HasStackTrace = 0x80;
        //  IsCustomType
        public const byte CustomTypeName = 0x20;

        #endregion

        #region Encoding

        static public void Encode(MorphWriter writer, bool encodeValue, object value)
            => Encode(writer, ref encodeValue, value, null);

        static public void Encode(MorphWriter writer, bool encodeValue, object value, string name)
            => Encode(writer, ref encodeValue, value, name);

        static private void Encode(MorphWriter writer, ref bool encodeValue, object value, string name)
        {
            //  Begin encoding the value
            byte valueType = 0;
            //  Has value name
            if (!string.IsNullOrEmpty(name))
            {
                valueType |= ValueType.HasValueName;
                writer.WriteIdentifier(name);
            }
            //  Has type name
            //  <Not implemented>
            //  IsNull
            if (value == null && encodeValue)
            {
                WriteByte(writer, valueType | ValueType.IsNull);
                return;
            }
            Type type = value.GetType();
            //  Simple type
            if (SimpleType.CanEncode(type, out EncodeSimpleType typeEncoder, out EncodeSimpleValue valueEncoder))
            {
                WriteByte(writer, IsSimpleType);
                typeEncoder(writer);
                if (encodeValue)
                    valueEncoder(writer, value);
            }
            else
            {
                if (type.IsArray) EncodeArray(writer, valueType, type, (Array)value);
                else if (value is Exception x) EncodeException(writer, valueType, type, x);
                else if (value is Stream stream) EncodeStream(writer, valueType, type, stream);
                else if (!type.IsClass) EncodeStruct(writer, valueType, type, value);
                else if (type.IsClass) EncodeServlet(writer, valueType, type, value);
                else EMorph.Throw(0, "Encoding data type not supported: " + value.GetType().FullName, null);
            }
        }

        static private void WriteByte(MorphWriter writer, int byteFlags)
            => writer.WriteInt8((byte)byteFlags);

        static private void EncodeArray(MorphWriter writer, byte valueType, Type type, Array array)
        {
            valueType |= ValueType.IsArray;
            //  Byte array is so common and optimisable, that here we do a special implementation.
            if (array is byte[] bytes)
            {
                WriteByte(writer, valueType | IsArrayElemType);
                WriteByte(writer, SimpleType.IsByte);   //  Element's data type
                writer.WriteInt32(bytes.Length);
                writer.WriteBytes(bytes);
                return;
            }
            Type elementType = type.GetElementType();
            //  Array of simple type
            if (SimpleType.CanEncode(elementType, out EncodeSimpleType typeEncoder, out EncodeSimpleValue valueEncoder))
            {
                WriteByte(writer, valueType | IsArrayElemType);
                typeEncoder(writer);    //  Element's data type
                writer.WriteInt32(array.Length);
                foreach (var value in array)
                    valueEncoder(writer, value);
                return;
            }
            //  A more complex type of array
            else
            {
                WriteByte(writer, valueType);
                writer.WriteInt32(array.Length);
                foreach (var value in array)
                    ValueType.Encode(writer, true, value);
                return;
            }
        }

        static private void EncodeException(MorphWriter writer, byte valueType, Type type, Exception value)
        {
            bool isEMorph = value is EMorph;
            string stackTrace = value.StackTrace;
            bool hasStackTrace = !string.IsNullOrEmpty(stackTrace);

            //  ValueType
            valueType |= IsException | HasTypeName;
            if (isEMorph) valueType |= HasExceptionCode;
            if (hasStackTrace) valueType |= HasStackTrace;
            writer.WriteInt8(valueType);

            //  Exception type/class
            writer.WriteIdentifier(type.Name);
            //  Exception code
            if (value is EMorph xMorph)
            {
                //  Error context (ex. Morph exception, Windows error, etc.)
                writer.WriteInt32(0);
                //  Actual error code (Always lowest 4 bytes, if it happens to be larger.)
                writer.WriteInt32(xMorph.ErrorCode);
            }
            //  Stack trace
            if (hasStackTrace)
                writer.WriteString(stackTrace);
        }

        static private void EncodeStream(MorphWriter writer, byte valueType, Type type, Stream value)
        {
            EMorph.Throw(0, "Streams not yet supported: " + type.FullName, null);
        }

        static private void EncodeStruct(MorphWriter writer, byte valueType, Type type, object value)
        {
            BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.GetField;
            var fields = type.GetFields(flags).Where((field) => !field.IsStatic && !field.IsLiteral);

            WriteByte(writer, valueType | IsStruct);
            writer.WriteInt32(fields.Count());
            foreach (FieldInfo field in fields)
                ValueType.EncodeValue(writer, true, field.GetValue(value), field.Name);
        }

        static private void EncodeServlet(MorphWriter writer, byte valueType, Type type, object value)
        {
            throw new EMorphInvocation("ValueType", "Data type not supported: " + type.FullName, "");
        }

        #endregion
    }

}