using Morph.Core;
using Morph.Endpoint;
using Morph.Internet;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.Remoting.Messaging;

namespace Morph.Params
{
    public class Parameters
    {
        #region ValueType

        //  Basic
        private const byte ValueType_HasValueName = 0x01;
        private const byte ValueType_IsNull = 0x02;
        private const byte ValueType_HasTypeName = 0x04;
        private const byte ValueType_IsValue = 0x00;
        private const byte ValueType_IsReference = 0x08;

        //  By reference
        private const byte ValueType_IsServlet = 0x10;
        //  Servlet
        private const byte ValueType_HasDevicePath = 0x20;
        private const byte ValueType_HasArrayIndex = 0x40;

        //  By value
        private const byte ValueType_ValueMask = 0x30;
        private const byte ValueType_IsSimpleType = 0x00;
        private const byte ValueType_IsArray = 0x10;
        private const byte ValueType_IsStruct = 0x20;

        private const byte ValueType_ArrayElemType = 0x40;

        #endregion

        #region Encoding

        static public MorphWriter Encode(object[] Params, InstanceFactories instanceFactories)
        {
            if (Params == null)
                return null;
            return Encode(Params, null, instanceFactories);
        }

        static public MorphWriter Encode(object[] Params, object special, InstanceFactories instanceFactories)
        {
            int paramCount = 1 + (Params == null ? 0 : Params.Length);
            MemoryStream stream = new MemoryStream();
            MorphWriter writer = new MorphWriter(stream);
            //  Write the param count
            writer.WriteInt32(paramCount);
            //  Write the "special" param (ie. return value, property value)
            EncodeValue(writer, instanceFactories, null, special, true, true);
            //  Write each param
            if (Params != null)
                foreach (object obj in Params)
                    EncodeValue(writer, instanceFactories, null, obj, true, true);
            //  Return the data
            stream.Close();
            return writer;
        }

        static private void WriteValueType(MorphWriter writer, byte valueType, string valueName, string typeName)
        {
            if (valueName != null) valueType |= ValueType_HasValueName;
            if (typeName != null) valueType |= ValueType_HasTypeName;
            writer.WriteInt8(valueType);
            if (valueName != null) writer.WriteIdentifier(valueName);
            if (typeName != null) writer.WriteIdentifier(typeName);
        }

        static private void EncodeValue(MorphWriter writer, InstanceFactories instanceFactories, string valueName, object value, bool encodeType, bool encodeValue)
        {
            //  IsNull
            if (value == null)
            {
                WriteValueType(writer, ValueType_IsNull, valueName, null);
                return;
            }

            //  Deal with reference types...
            //  IsServlet
            if (value is IMorphReference morphReference)
            {
                if (morphReference.MorphServlet == null)
                    throw new EMorph("Morph Reference needs a Morph Servlet");
                value = morphReference.MorphServlet;
            }
            if (value is Servlet)
            {
                EncodeServlet(writer, valueName, value, encodeType, encodeValue);
                return;
            }
            if (value is ServletProxy)
            {
                EncodeServletProxy(writer, valueName, value, encodeType, encodeValue);
                return;
            }
            //  IsStream
            if (value is ValueStream)
            {
                WriteValueType(writer, ValueType_IsReference, valueName, null);
                return;
            }

            //  Deal with value/instance types... (ie. non-reference types)
            //  Value instance
            ValueInstance valueInstance = null;
            ////  - Developer defined encoding
            //if (instanceFactories.EncodeInstance(value, ref valueInstance, out string typeName))
            //{
            //    WriteValueType(writer, ValueType_IsValue|ValueType_IsCustomType , valueName, typeName);
            //    EncodeValueInstance(writer, instanceFactories, valueInstance, valueName);
            //    return;
            //}
            if (value is IMorphInstance morphInstance)
                valueInstance = morphInstance.MorphEncode();
            //  - Already encoded
            else if (value is ValueInstance)
                valueInstance = (ValueInstance)value;
            if (valueInstance != null)
            {
                EncodeValueInstance(writer, instanceFactories, valueInstance, valueName, encodeType, encodeValue);
                return;
            }
            //  Struct and array
            //  SimpleType
            if (SimpleType.GetEncoder(value.GetType(), out SimpleType.Encoder encoder))
            {
                WriteValueType(writer, ValueType_IsValue | ValueType_IsSimpleType, valueName, null);
                encoder(writer, value, encodeType, encodeValue);
                return;
            }
            if (value is System.Array)
            {
                EncodeArray(writer, instanceFactories, value, valueName, encodeType, encodeValue);
                return;
            }
            else
            {
                EncodeStruct(writer, instanceFactories, value, valueName, encodeType, encodeValue);
                return;
            }
            //  Failed
            throw new EMorph("Encoding of parameter type " + value.GetType().FullName + " is not implemented.");
        }

        static private void EncodeServlet(MorphWriter writer, string valueName, object value, bool encodeType, bool encodeValue)
        {
            Servlet servlet = (Servlet)value;
            //  ValueType
            if (encodeType)
                WriteValueType(writer, ValueType_IsReference | ValueType_IsServlet, valueName, servlet.TypeName);
            //  Value
            if (encodeValue)
            {
                //  IsReference
                writer.WriteInt32(servlet.ID);
                //  IsServlet
                writer.WriteInt32(servlet.Apartment.ID);
            }
        }

        static private void EncodeServletProxy(MorphWriter writer, string valueName, object value, bool encodeType, bool encodeValue)
        {
            ServletProxy servlet = (ServletProxy)value;
            LinkStack devicePath = servlet.ApartmentProxy.Device.Path;
            //  ValueType
            byte valueType = ValueType_IsReference | ValueType_IsServlet;
            if (devicePath != null) valueType |= ValueType_HasDevicePath;
            if (encodeType)
                WriteValueType(writer, valueType, valueName, servlet.TypeName);
            //  Value
            if (encodeValue)
            {
                //  IsReference
                writer.WriteInt32(servlet.ID);
                //  IsServlet
                writer.WriteInt32(servlet.ApartmentProxy.ApartmentLink.ApartmentID);
                //  HasDevicePath
                if (devicePath != null)
                {
                    writer.WriteInt32(devicePath.ByteSize);
                    devicePath.Write(writer);
                }
                //  Array index

            }
        }

        static private void EncodeValueInstance(MorphWriter writer, InstanceFactories instanceFactories, ValueInstance value, string valueName, bool encodeType, bool encodeValue)
        {
            //  Write Struct
            if (value.Struct != null)
            {
                //  ValueType
                if (encodeType)

                    WriteValueType(writer, ValueType_IsValue | ValueType_IsStruct, valueName, value.TypeName);
                //  Value
                if (encodeValue)
                {
                    StructValues Struct = value.Struct;
                    //  ValueCount
                    writer.WriteInt32(Struct.Count);
                    //  Values
                    for (int i = 0; i < Struct.Count; i++)
                        EncodeValue(writer, instanceFactories, Struct.Names[i], Struct.Values[i], encodeType, encodeValue);
                }
            }
            //  Write Array
            if (value.Array != null)
            {
                //  ValueType
                if (encodeType)
                    WriteValueType(writer, ValueType_IsValue | ValueType_IsArray, valueName, value.TypeName);
                //  Value
                if (encodeValue)
                {
                    ArrayValues array = value.Array;
                    //  ArrayCount
                    writer.WriteInt32(array.Count);
                    //  Values
                    for (int i = 0; i < array.Count; i++)
                        EncodeValue(writer, instanceFactories, null, array.Values[i], encodeType, encodeValue);
                }
            }
        }

        static private void EncodeArray(MorphWriter writer, InstanceFactories instanceFactories, object value, string valueName, bool encodeType, bool encodeValue)
        {
            Array array = (Array)value;
            Type elementType = array.GetType().GetElementType();
            bool useArrayElemType = array.Length > 0 && (elementType.IsValueType || elementType == typeof(string));
            //  ValueType
            if (encodeType)
            {
                byte valueType = ValueType_IsValue | ValueType_IsArray;
                valueType |= useArrayElemType ? ValueType_ArrayElemType : (byte)0;
                WriteValueType(writer, valueType, valueName, value.GetType().Name);
            }
            //  Value
            if (encodeValue)
            {
                //  ArrayElemCount
                writer.WriteInt32(array.Length);
                //  ArrayElemType
                if (useArrayElemType)
                    EncodeValue(writer, instanceFactories, null, null, true, false);
                //  Write values
                for (int i = 0; i < array.Length; i++)
                    EncodeValue(writer, instanceFactories, null, array.GetValue(i), !useArrayElemType, true);
            }
        }

        static private void EncodeStruct(MorphWriter writer, InstanceFactories instanceFactories, object value, string valueName, bool encodeType, bool encodeValue)
        {
            //  ValueType
            if (encodeType)
                WriteValueType(writer, ValueType_IsValue | ValueType_IsStruct, valueName, value.GetType().Name);
            //  Value
            if (encodeValue)
            {
                FieldInfo[] fields = value.GetType().GetFields();
                fields = Array.FindAll(fields, field => field.IsPublic && !field.IsStatic && !field.IsLiteral);
                //  StructElemCount
                writer.WriteInt32(fields.Length);
                //  Values
                foreach (FieldInfo field in fields)
                    if (field.IsPublic && !field.IsStatic && !field.IsLiteral)
                        EncodeValue(writer, instanceFactories, field.Name, field.GetValue(value), encodeType, encodeValue);
            }
        }

        #endregion

        #region Decoding

        static public void Decode(InstanceFactories instanceFactories, LinkStack devicePath, MorphReader dataReader, out object[] Params, out object special)
        {
            if ((dataReader == null) || !dataReader.CanRead)
            {
                Params = null;
                special = null;
                return;
            }
            //  Param count
            int paramCount = dataReader.ReadInt32() - 1;
            //  Read in special
            special = DecodeValue(instanceFactories, devicePath, dataReader, out string Name);
            //  Read in parameters
            if (paramCount > 0)
            {
                Params = new object[paramCount];
                for (int i = 0; i < Params.Length; i++)
                    Params[i] = DecodeValue(instanceFactories, devicePath, dataReader, out Name);
            }
            else
                Params = null;
        }

        static private byte ReadValueType(MorphReader reader, out string valueName, out string typeName)
        {
            byte valueType = (byte)reader.ReadInt8();
            valueName = null;
            typeName = null;
            if ((valueType & ValueType_HasValueName) != 0) valueName = reader.ReadIdentifier();
            if ((valueType & ValueType_HasTypeName) != 0) typeName = reader.ReadIdentifier();
            return valueType;
        }

        static private object DecodeValue(InstanceFactories instanceFactories, LinkStack devicePath, MorphReader reader, out string valueName)
        {
            byte valueType = (byte)reader.ReadInt8();
            return DecodeValue(instanceFactories, devicePath, reader, valueType, out valueName);
        }

        static private object DecodeValue(InstanceFactories instanceFactories, LinkStack devicePath, MorphReader reader, byte valueType, out string valueName)
        {
            valueName = null;
            //  HasValueName
            if ((valueType & ValueType_HasValueName) != 0)
                valueName = reader.ReadIdentifier();
            //  IsNull
            if ((valueType & ValueType_IsNull) != 0)
                return null;
            //  HasTypeName
            string typeName = null;
            if ((valueType & ValueType_HasTypeName) != 0)
                typeName = reader.ReadIdentifier();

            //  Deal with reference types...
            //  IsReference
            if ((valueType & ValueType_IsReference) != 0)
                //  IsServlet
                if ((valueType & ValueType_IsServlet) != 0)
                    return DecodeServlet(instanceFactories, devicePath, reader, valueType, typeName);
                //  Is stream
                else
                    throw new EMorph("Not implemented");

            //  Deal with value/instance types... (ie. non-reference types)
            //  IsStruct or IsArray
            bool isArray = (valueType & ValueType_IsArray) != 0;
            bool isStruct = (valueType & ValueType_IsStruct) != 0;
            if (isStruct || isArray)
            {
                ValueInstance value = (ValueInstance)DecodeValueInstance(instanceFactories, devicePath, reader, valueType, isStruct, isArray, typeName);
                object complexResult;
                //  Might be able to translate to a proper instance
                if (instanceFactories.DecodeInstance(value, out complexResult))
                    return complexResult;
                if (isArray && !isStruct)
                    return value.Array.Values.ToArray();
                return value;
            }
            //  Is simple type
            object simpleResult = SimpleType.Decode(reader);
            instanceFactories.DecodeSimple(simpleResult, typeName, out simpleResult);
            return simpleResult;
        }

        static private object DecodeServlet(InstanceFactories instanceFactories, LinkStack devicePath, MorphReader reader, byte valueType, string typeName)
        {
            //  Servlet ID
            int servletID = reader.ReadInt32();
            //  MorphApartment ID
            int apartmentID = reader.ReadInt32();
            //  Device path
            LinkStack fullPath;
            if ((valueType & ValueType_HasDevicePath) != 0)
                fullPath = new LinkStack(reader.ReadBytes(reader.ReadInt32()));
            else
                fullPath = new LinkStack();
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
              ((links.Count == 1) && (links[0] is LinkInternet) && (Connections.IsEndPointOnThisProcess(((LinkInternet)links[0]).EndPoint))))
            {
                MorphApartment apartment = MorphApartmentFactory.Find(apartmentID);
                if (apartment == null)
                    throw new EMorph("Apartment not found");
                //  Create servlet proxy
                Servlet servlet = apartment.Servlets.Find(servletID);
                if (servlet == null)
                    throw new EMorph("Servlet not found");
                return servlet.Object;
            }
            //  Obtain servlet proxy
            ServletProxy proxy = Devices.Obtain(fullPath).Obtain(apartmentID, instanceFactories).ServletProxies.Obtain(servletID, typeName);
            //  Try to convert it 
            if ((instanceFactories != null) && (instanceFactories.DecodeReference(proxy, out object result)))
                return result;
            else
                return proxy;
        }

        static private object DecodeValueInstance(InstanceFactories instanceFactories, LinkStack devicePath, MorphReader reader, byte valueType, bool isStruct, bool isArray, string typeName)
        {
            //  Create result
            ValueInstance result = new ValueInstance(typeName, isStruct, isArray);
            //  Read Array
            if (isArray)
            {
                //  ArrayElemCount
                int arrayElemCount = reader.ReadInt32();
                //  ArrayElemType
                if ((valueType & ValueType_ArrayElemType) != 0)
                {
                    byte arrayElemType = ReadValueType(reader, out string _, out string arrayElemTypeName);
                    switch (arrayElemType & ValueType_ValueMask)
                    {
                        case ValueType_IsSimpleType:
                            Type simpleType = SimpleType.DecodeType(reader);
                            //  byte[] is especially optimisable, so lets do that here
                            if (simpleType == typeof(bool))
                                return reader.ReadBytes(arrayElemCount);
                            //  Read in an array of simple values
                            SimpleType.Decoder decoder = SimpleType.GetDecoder(arrayElemType);
                            object[] simpleArray = new object[arrayElemCount];
                            for (int i = 0; i < arrayElemCount; i++)
                                simpleArray[i] = decoder(reader, true);
                            return simpleArray;

                        case ValueType_IsArray:
                        case ValueType_IsStruct:
                            //  Read in array of complex instance types
                            object[] complexArray = new object[arrayElemCount];
                            for (int i = 0; i < arrayElemCount; i++)
                                complexArray[i] = DecodeValueInstance(instanceFactories, devicePath, reader, arrayElemType, false, true, arrayElemTypeName);
                            return complexArray;

                        default:
                            throw new EMorph(0, $"Unsupported ValueType: {arrayElemType}");
                    }
                }
                //  Read values, each one with it's own type
                object[] array = new object[arrayElemCount];
                for (int i = 0; i < arrayElemCount; i++)
                    array[i] = DecodeValue(instanceFactories, devicePath, reader, out string _);
            }
            //  Read Struct
            if (isStruct)
            {
                StructValues Struct = result.Struct;
                //  StructElemCount
                int structElemCount = reader.ReadInt32();
                //  Read values
                for (int i = 0; i < structElemCount; i++)
                {
                    object Value = DecodeValue(instanceFactories, devicePath, reader, out string Name);
                    Struct.Add(Value, Name);
                }
            }
            return result;
        }

        #endregion
    }
}