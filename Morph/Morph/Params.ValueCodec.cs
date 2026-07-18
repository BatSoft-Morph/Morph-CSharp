using Morph.Core;
using Morph.Endpoint;
using Morph.Internet;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace Morph.Params
{
    /// <summary>
    /// <para>Encodes and decodes a single value:  the ValueType byte, its names, and its payload.</para>
    /// <para>Structure per "Morph Protocol.xlsx" tab "ValueType".</para>
    /// </summary>
    internal static class ValueCodec
    {
        #region Encoding

        static public void EncodeValue(MorphWriter writer, InstanceFactories instanceFactories, string valueName, object value)
        {
            //  Null:  a ValueType byte with IsNull set, and nothing more
            if (value == null)
            {
                WriteValueType(writer, ValueType.IsNull, null, valueName);
                return;
            }

            //  References...
            if (value is IMorphReference morphReference)
            {
                if (morphReference.MorphServlet == null)
                    throw new EMorph("Morph Reference needs a Morph Servlet");
                value = morphReference.MorphServlet;
            }
            if (value is Servlet servlet)
            {
                EncodeServlet(writer, valueName, servlet);
                return;
            }
            if (value is ServletProxy servletProxy)
            {
                EncodeServletProxy(writer, valueName, servletProxy);
                return;
            }
            if (value is ValueStream)
                throw new EMorph("Stream references are not yet defined in the Morph Protocol");

            //  Values...
            Type type = value.GetType();
            //  Simple
            if (SimpleType.TryGetCodec(type, out byte simpleTypeByte, out SimpleType.ValueWriter writeValue))
            {
                WriteValueType(writer, ValueType.IsSimple, null, valueName);
                writer.WriteInt8(simpleTypeByte);
                writeValue(writer, value);
                return;
            }
            //  Enum (a simple type whose payload carries the enum type name)
            if (type.IsEnum)
            {
                WriteValueType(writer, ValueType.IsSimple, null, valueName);
                writer.WriteInt8(SimpleType.EnumSimpleTypeByte(type));
                SimpleType.WriteEnumValue(writer, value);
                return;
            }
            //  Custom (developer defined encoding to a named simple value)
            if ((instanceFactories != null) && instanceFactories.EncodeSimple(out object customValue, out string customTypeName, value))
            {
                EncodeCustom(writer, valueName, customValue, customTypeName);
                return;
            }
            //  Instances that describe their own encoding
            ValueInstance valueInstance = null;
            if (value is IMorphInstance morphInstance)
                valueInstance = morphInstance.MorphEncode();
            else if (value is ValueInstance alreadyEncoded)
                valueInstance = alreadyEncoded;
            else if (instanceFactories != null)
                instanceFactories.EncodeInstance(value, ref valueInstance, out string _);
            if (valueInstance != null)
            {
                EncodeValueInstance(writer, instanceFactories, valueName, valueInstance);
                return;
            }
            //  Array
            if (value is Array array)
            {
                EncodeArray(writer, instanceFactories, valueName, array);
                return;
            }
            //  Struct:  any other type is reflected over its public fields
            EncodeStruct(writer, instanceFactories, valueName, value);
        }

        static private void WriteValueType(MorphWriter writer, byte valueType, string typeName, string valueName)
        {
            if (typeName != null) valueType |= ValueType.HasTypeName;
            if (valueName != null) valueType |= ValueType.HasValueName;
            writer.WriteInt8(valueType);
            if (typeName != null) writer.WriteIdentifier(typeName);
            if (valueName != null) writer.WriteIdentifier(valueName);
        }

        static private void EncodeCustom(MorphWriter writer, string valueName, object customValue, string customTypeName)
        {
            if (customTypeName == null)
                throw new EMorphUsage("A custom type must have a type name");
            if (!SimpleType.TryGetCodec(customValue.GetType(), out byte simpleTypeByte, out SimpleType.ValueWriter writeValue))
                throw new EMorphUsage("A custom type must encode to a simple value");
            WriteValueType(writer, ValueType.IsCustom, customTypeName, valueName);
            writer.WriteInt8(simpleTypeByte);
            writeValue(writer, customValue);
        }

        static private void EncodeValueInstance(MorphWriter writer, InstanceFactories instanceFactories, string valueName, ValueInstance value)
        {
            if (value.Struct != null)
            {
                WriteValueType(writer, ValueType.IsStruct, value.TypeName, valueName);
                StructValues structValues = value.Struct;
                //  StructElemCount
                writer.WriteInt32(structValues.Count);
                //  Values, each with its own ValueType
                for (int i = 0; i < structValues.Count; i++)
                    EncodeValue(writer, instanceFactories, structValues.Names[i], structValues.Values[i]);
            }
            else if (value.Array != null)
            {
                WriteValueType(writer, ValueType.IsArray, value.TypeName, valueName);
                ArrayValues arrayValues = value.Array;
                //  ArrayElemCount
                writer.WriteInt32(arrayValues.Count);
                //  Values, each with its own ValueType
                for (int i = 0; i < arrayValues.Count; i++)
                    EncodeValue(writer, instanceFactories, null, arrayValues.Values[i]);
            }
            else
                throw new EMorphUsage("A ValueInstance must be a struct or an array");
        }

        static private void EncodeArray(MorphWriter writer, InstanceFactories instanceFactories, string valueName, Array array)
        {
            //  TODO: Morph represents a multidimensional array as nested arrays - one array ValueType
            //  per dimension - so an N-dimensional CLR array should encode as N nested arrays rather
            //  than being rejected.  Until that is implemented, reject here (before writing anything, so
            //  a failure does not corrupt the message being built).  See Specifications/Backlog.md.
            if (array.Rank != 1)
                throw new EMorph("Multidimensional arrays are not yet implemented on this side");
            Type elementType = array.GetType().GetElementType();
            //  Non-nullable simple elements are all of the one type, so per-element ValueTypes can be omitted
            byte elemSimpleType = 0;
            SimpleType.ValueWriter writeElem = null;
            bool useArrayElemType =
                elementType.IsValueType &&
                (Nullable.GetUnderlyingType(elementType) == null) &&
                SimpleType.TryGetCodec(elementType, out elemSimpleType, out writeElem);
            byte valueTypeByte = ValueType.IsArray;
            if (useArrayElemType)
                valueTypeByte |= ValueType.IsArrayElemType;
            WriteValueType(writer, valueTypeByte, array.GetType().Name, valueName);
            //  ArrayElemType:  the element type, without values  (a type, so encoding stops after the type bytes)
            if (useArrayElemType)
            {
                writer.WriteInt8(ValueType.IsSimple);
                writer.WriteInt8(elemSimpleType);
            }
            //  ArrayElemCount
            writer.WriteInt32(array.Length);
            //  Values
            //  (The byte[] check must be exact:  arrays of byte backed enums satisfy "is byte[]"
            //   through CLR array covariance, but must encode as per-element enum values.)
            if (array.GetType() == typeof(byte[]))
                writer.WriteBytes((byte[])array);
            else if (useArrayElemType)
                for (int i = 0; i < array.Length; i++)
                    writeElem(writer, array.GetValue(i));
            else
                for (int i = 0; i < array.Length; i++)
                    EncodeValue(writer, instanceFactories, null, array.GetValue(i));
        }

        static private void EncodeStruct(MorphWriter writer, InstanceFactories instanceFactories, string valueName, object value)
        {
            WriteValueType(writer, ValueType.IsStruct, value.GetType().Name, valueName);
            FieldInfo[] fields = Array.FindAll(value.GetType().GetFields(),
                field => field.IsPublic && !field.IsStatic && !field.IsLiteral);
            //  StructElemCount
            writer.WriteInt32(fields.Length);
            //  Values, each with its own ValueType
            foreach (FieldInfo field in fields)
                EncodeValue(writer, instanceFactories, field.Name, field.GetValue(value));
        }

        #endregion

        #region Encoding references

        static private void EncodeServlet(MorphWriter writer, string valueName, Servlet servlet)
        {
            WriteValueType(writer, ValueType.IsReference | ValueType.IsServlet, servlet.TypeName, valueName);
            //  ReferenceID
            writer.WriteInt32(servlet.ID);
            //  ApartmentID
            writer.WriteInt32(servlet.Apartment.ID);
        }

        static private void EncodeServletProxy(MorphWriter writer, string valueName, ServletProxy servletProxy)
        {
            LinkStack devicePath = servletProxy.ApartmentProxy.Device.Path;
            byte valueTypeByte = ValueType.IsReference | ValueType.IsServlet;
            if (devicePath != null)
                valueTypeByte |= ValueType.HasDevicePath;
            WriteValueType(writer, valueTypeByte, servletProxy.TypeName, valueName);
            //  ReferenceID
            writer.WriteInt32(servletProxy.ID);
            //  ApartmentID
            writer.WriteInt32(servletProxy.ApartmentProxy.ApartmentLink.ApartmentID);
            //  DevicePath
            if (devicePath != null)
            {
                writer.WriteInt32(devicePath.ByteSize);
                devicePath.Write(writer);
            }
        }

        #endregion

        #region Decoding

        static public object DecodeValue(MorphReader reader, InstanceFactories instanceFactories, LinkStack devicePath)
            => DecodeValue(reader, instanceFactories, devicePath, out string _);

        static public object DecodeValue(MorphReader reader, InstanceFactories instanceFactories, LinkStack devicePath, out string valueName)
        {
            byte valueTypeByte = reader.ReadInt8();
            string typeName = (valueTypeByte & ValueType.HasTypeName) != 0 ? reader.ReadIdentifier() : null;
            valueName = (valueTypeByte & ValueType.HasValueName) != 0 ? reader.ReadIdentifier() : null;
            //  Null:  nothing follows the names, whatever the other flags say
            if ((valueTypeByte & ValueType.IsNull) != 0)
                return null;
            //  References
            if ((valueTypeByte & ValueType.IsReference) != 0)
            {
                int referenceID = reader.ReadInt32();
                if ((valueTypeByte & ValueType.IsServlet) != 0)
                    return DecodeServlet(reader, instanceFactories, devicePath, valueTypeByte, typeName, referenceID);
                throw new EMorph("Stream references are not yet defined in the Morph Protocol");
            }
            //  Values
            switch (valueTypeByte & ValueType.ValueKindMask)
            {
                case ValueType.IsSimple:
                case ValueType.IsCustom:
                    {
                        object simple = SimpleType.ReadValue(reader, instanceFactories);
                        if (instanceFactories != null)
                            instanceFactories.DecodeSimple(simple, typeName, out simple);
                        return simple;
                    }
                case ValueType.IsArray:
                    return DecodeArray(reader, instanceFactories, devicePath, valueTypeByte, typeName);
                default:    //  ValueType.IsStruct
                    return DecodeStruct(reader, instanceFactories, devicePath, typeName);
            }
        }

        static private object DecodeArray(MorphReader reader, InstanceFactories instanceFactories, LinkStack devicePath, byte valueTypeByte, string typeName)
        {
            //  ArrayElemType
            byte elemSimpleType = 0;
            bool hasElemType = (valueTypeByte & ValueType.IsArrayElemType) != 0;
            if (hasElemType)
            {
                byte elemValueType = reader.ReadInt8();
                if ((elemValueType & ValueType.HasTypeName) != 0) reader.ReadIdentifier();
                if ((elemValueType & ValueType.HasValueName) != 0) reader.ReadIdentifier();
                //  IsNull is ignored when decoding a type
                if (((elemValueType & ValueType.IsReference) != 0) ||
                    ((elemValueType & ValueType.ValueKindMask) != ValueType.IsSimple))
                    throw new EMorph("Array element types must be simple on this implementation");
                elemSimpleType = reader.ReadInt8();
            }
            //  ArrayElemCount
            int count = reader.ReadInt32();
            //  Values
            if (hasElemType)
            {
                //  byte[] fast path
                if (elemSimpleType == (SimpleType.TypeInteger | SimpleType.Size1Byte))
                    return reader.ReadBytes(count);
                //  The wire declares the element type, so rebuild the typed array directly
                Type elemClrType = SimpleType.ToClrType(elemSimpleType);
                if (elemClrType != null)
                {
                    Array typedArray = Array.CreateInstance(elemClrType, count);
                    for (int i = 0; i < count; i++)
                        typedArray.SetValue(SimpleType.ReadValue(reader, elemSimpleType, instanceFactories), i);
                    return typedArray;
                }
            }
            ValueInstance result = new ValueInstance(typeName, false, true);
            for (int i = 0; i < count; i++)
                if (hasElemType)
                    result.Array.Add(SimpleType.ReadValue(reader, elemSimpleType, instanceFactories));
                else
                    result.Array.Add(DecodeValue(reader, instanceFactories, devicePath));
            //  An instance factory might turn it into a proper typed instance
            if ((instanceFactories != null) && instanceFactories.DecodeInstance(result, out object instance))
                return instance;
            //  Rebuild the array type the sender declared, when that is evident
            return RebuildDeclaredArray(result.Array.Values.ToArray(), typeName);
        }

        /// <summary>
        /// <para>Turns an object[] of decoded elements back into the array type the sender declared
        /// (ex. "String[]"), so that methods expecting typed arrays can be invoked.</para>
        /// <para>Rebuilds only when the evidence agrees:  every element is of the one type named by
        /// the wire TypeName.  Otherwise the object[] is returned unchanged.</para>
        /// </summary>
        static private object RebuildDeclaredArray(object[] values, string typeName)
        {
            if (typeName == null)
                return values;
            //  The one shared element type, judged from the non-null elements
            Type elementType = null;
            foreach (object value in values)
                if (value != null)
                {
                    Type valueType = value.GetType();
                    if (elementType == null)
                        elementType = valueType;
                    else if (elementType != valueType)
                        return values;
                }
            if (elementType == null)
                return values;      //  All null:  nothing to infer from
            //  Null elements require a reference element type
            if (elementType.IsValueType)
                foreach (object value in values)
                    if (value == null)
                        return values;
            //  Only rebuild what the sender declared
            if (typeName != elementType.Name + "[]")
                return values;
            Array typedArray = Array.CreateInstance(elementType, values.Length);
            values.CopyTo(typedArray, 0);
            return typedArray;
        }

        static private object DecodeStruct(MorphReader reader, InstanceFactories instanceFactories, LinkStack devicePath, string typeName)
        {
            //  StructElemCount
            int count = reader.ReadInt32();
            //  Values
            ValueInstance result = new ValueInstance(typeName, true, false);
            for (int i = 0; i < count; i++)
            {
                object value = DecodeValue(reader, instanceFactories, devicePath, out string name);
                result.Struct.Add(value, name);
            }
            //  An instance factory might turn it into a proper typed instance
            if ((instanceFactories != null) && instanceFactories.DecodeInstance(result, out object instance))
                return instance;
            return result;
        }

        #endregion

        #region Decoding references

        static private object DecodeServlet(MorphReader reader, InstanceFactories instanceFactories, LinkStack devicePath, byte valueTypeByte, string typeName, int servletID)
        {
            //  ApartmentID
            int apartmentID = reader.ReadInt32();
            //  DevicePath
            LinkStack fullPath;
            if ((valueTypeByte & ValueType.HasDevicePath) != 0)
                fullPath = new LinkStack(reader.ReadBytes(reader.ReadInt32()));
            else
                fullPath = new LinkStack();
            //  Index
            if ((valueTypeByte & ValueType.HasArrayIndex) != 0)
                throw new EMorph("Array indexed servlet references are not supported on this implementation");
            //  Add the device path to make it the point of view of this receiver.
            fullPath.Push(devicePath);
            //  Optimise device path
            List<Link> links = fullPath.ToLinks();
            //  Eliminate: A-B-A  ( -> A-A )
            for (int i = links.Count - 3; i >= 0; i--)
                if (links[i].Equals(links[i + 2]))
                    links.RemoveAt(i + 1);
            //  Eliminate: A-A    ( -> A )
            for (int i = links.Count - 2; i >= 0; i--)
                if (links[i].Equals(links[i + 1]))
                    links.RemoveAt(i + 1);
            //  Might be in a local apartment
            if ((links.Count == 0) ||
              ((links.Count == 1) && (links[0] is LinkInternet linkInternet) && Connections.IsEndPointOnThisProcess(linkInternet.EndPoint)))
            {
                MorphApartment apartment = MorphApartmentFactory.Find(apartmentID);
                if (apartment == null)
                    throw new EMorph("Apartment not found");
                //  Find the servlet itself
                Servlet servlet = apartment.Servlets.Find(servletID);
                if (servlet == null)
                    throw new EMorph("Servlet not found");
                return servlet.Object;
            }
            //  Obtain servlet proxy
            ServletProxy proxy = Devices.Obtain(fullPath).Obtain(apartmentID, instanceFactories).ServletProxies.Obtain(servletID, typeName);
            //  Try to convert it to a business object facade
            if ((instanceFactories != null) && instanceFactories.DecodeReference(proxy, out object result))
                return result;
            return proxy;
        }

        #endregion
    }
}
