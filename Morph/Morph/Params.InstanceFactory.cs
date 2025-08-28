/**
 * When messages arrive at an endpoint, the endpoint will usually need to translate
 * parameters into actual instances.  That is the role of instance factories.
 * 
 * Note:  The class InstanceFactories is not thread safe, so add all 
 * factories to it before supplying it to a service or apartment proxy.
 * Instance factories need to be thread safe, though this will rarely be an issue.
 * 
 * Note:  Because structs are so limited in functionality, a general solution to 
 * handle all structs is possible (see class InstanceFactoryStruct).
 * On the other hand, class instances may be too compex due to differences
 * in constructors, internal behaviour with getters and setters and so on.
 * In these cases developers will have to make their own IInstanceFactory's that 
 * can take into account the complexity of each class.
 */

using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Morph.Endpoint;

namespace Morph.Params
{
    /// <summary>
    /// <para>An IReferenceFactory creates a business object proxy to encapsulate a servlet.</para>
    /// <example>Example:
    /// <code>
    ///  public class MyInterfaceProxy : MyInterface
    ///  {
    ///    internal MyInterfaceProxy(ServletProxy ServletProxy)
    ///    {
    ///      _ServletProxy = ServletProxy;
    ///    }
    /// 
    ///    private ServletProxy _ServletProxy;
    /// 
    ///    #region MyInterface Members
    /// 
    ///    public AnyType1 myMethod1(AnyType2 anyParam2, AnyType3 anyParam3)
    ///    {
    ///      return (AnyType1)_ServletProxy.CallMethod("myMethod1", new object[1] { anyParam2, anyParam3 });
    ///    }
    /// 
    ///    public AnyType3 myProperty1
    ///    {
    ///      get { return (AnyType3)_ServletProxy.CallGetProperty("myProperty1", null); }
    ///    }
    /// 
    ///    #endregion
    ///  }
    /// </code></example>
    /// </summary>
    public interface IReferenceDecoder
    {
        bool DecodeReference(ServletProxy value, out object reference);
    }

    /// <summary>
    /// <para>Tries to encode Instance to ValueInstance.  If not, then returns null.</para>
    /// <para>This is similar to IMorphInstance.  The only difference is that with this
    ///  the Instance object can be of any type, even a finalised type.</para>
    /// </summary>
    public interface IInstanceEncoder
    {
        ValueInstance EncodeInstance(object instance);
    }

    /// <summary>
    /// <para>An IInstanceFactory creates an instance based on the supplied Value.</para>
    /// <para>This is really the inverse of IMorphInstance.  A complex object can
    ///  encode itself using IMorphInstance.MorphEncode(), then on the
    ///  receiving side, an IInstanceFactory can convert the ValueInstance
    ///  back to the object as desired.</para>
    /// </summary>
    public interface IInstanceDecoder
    {
        bool DecodeInstance(ValueInstance value, out object instance);
    }

    /// <summary>
    /// <para>A simple factory has the same purpose as IInstanceFactory,<br/>
    /// except that the Value parameter is limited to the following simple types:<br/>
    /// - Int8 (Value)<br/>
    /// - Int16<br/>
    /// - Int32<br/>
    /// - Int64<br/>
    /// - Char<br/>
    /// - String<br/>
    /// - array of any of the above types.</para>
    /// <para>For examples, see the implementations of predefined types.</para>
    /// </summary>
    public interface ISimpleFactory
    {
        bool EncodeSimple(out object value, out string typeName, object instance);
        bool DecodeSimple(object value, string typeName, out object instance);
    }

    #region Useful general implementations

    /// <summary>
    ///  <para>This is meant to be commonly used for handling struct types automatically.<br/>
    ///  Add the struct types using AddStructType() and add to an InstanceFactories.</para>
    ///  <para>Note:  This is intended to work only for struct types.  However, it also works
    ///  for class types that look like structs.  This means having:<br/>
    ///  - a contructor without parameters<br/>
    ///  - fields (InstanceFactoryStruct ignores properties)</para>
    ///  <para>Examples:<br/>
    ///   <c>AddStructType(typeof(MyStruct));</c><br/>
    ///   <c>AddStructType(typeof(MyClass));</c></para>
    /// </summary>
    public class InstanceFactoryStruct : IInstanceDecoder
    {
        #region Private

        private readonly Hashtable _types = new Hashtable();

        private Type FindType(string valueTypeName)
        {
            if (valueTypeName == null)
                return null;
            else
                return (Type)_types[valueTypeName];
        }

        #endregion

        public void AddStructType(Type type)
        {
            _types.Add(type.Name, type);
        }

        #region IInstanceFactory Members

        private static readonly Type[] s_noParams = new Type[0];

        public bool DecodeInstance(ValueInstance value, out object instance)
        {
            instance = null;
            //  Find a type we are able to convert to
            Type type = FindType(value.TypeName);
            if (type == null)
                return false;
            //  Create the result instance
            ConstructorInfo constructor = type.GetConstructor(s_noParams);
            if (constructor != null)
                instance = constructor.Invoke(null);
            else
                instance = Activator.CreateInstance(type);
            //  Read the parameters into the instance
            for (int i = value.Struct.Count - 1; i >= 0; i--)
                type.GetField(value.Struct.Names[i]).SetValue(instance, value.Struct.Values[i]);
            //  Return success
            return true;
        }

        #endregion
    }

    /// <summary>
    ///  <para>This is meant to be commonly used for handling array types automatically.<br/>
    ///  Add the array element type using AddArrayElemType() and add to an InstanceFactories.</para>
    ///  <example>Examples:<br/>
    ///   <c>AddArrayElemType(typeof(int));  //  Converts to int[]</c><br/>
    ///   <c>AddArrayElemType(typeof(MyClass));  //  Converts to MyClass[]</c></example>
    /// </summary>
    public class InstanceFactoryArray : IInstanceDecoder
    {
        #region Private

        private readonly Hashtable _types = new Hashtable();

        private Type FindType(string valueTypeName)
        {
            if (valueTypeName == null)
                return null;
            else
                return (Type)_types[valueTypeName];
        }

        #endregion

        public void AddArrayElemType(Type type)
        {
            _types.Add(type.Name + "[]", type);
        }

        #region IInstanceFactory Members

        public bool DecodeInstance(ValueInstance value, out object instance)
        {
            instance = null;
            //  This factory only deals with "pure" arrays
            if ((value.Array == null) || (value.Struct != null))
                return false;
            //  Find a type we are able to convert to
            Type type = FindType(value.TypeName);
            if (type == null)
                return false;
            //  Get as object[]
            object[] values = value.Array.Values.ToArray();
            //  Convert to <type>[]
            Array array = Array.CreateInstance(type, values.Length);
            values.CopyTo(array, 0);
            //  Complete
            instance = array;
            return true;
        }

        #endregion
    }

    #endregion

    #region Predefined types

    internal class SimpleFactoryBool : ISimpleFactory
    {
        #region ISimpleFactory Members

        private const string TypeNameBool = "Bool";
        private const Byte True = 0xFF;
        private const Byte False = 0x00;

        public bool EncodeSimple(out object value, out string typeName, object instance)
        {
            typeName = TypeNameBool;
            if (instance is Boolean isTrue)
            {
                if (isTrue)
                    value = True; //  True as Byte 
                else
                    value = False; //  False as Byte 
                return true;
            }
            else
            {
                value = null;
                return false;
            }
        }

        public bool DecodeSimple(object value, string typeName, out object instance)
        {
            if (TypeNameBool.Equals(typeName))
            {
                instance = (Byte)value != 0;
                return true;
            }
            else
            {
                instance = null;
                return false;
            }
        }

        #endregion
    }

    internal class SimpleFactoryDateTime : ISimpleFactory
    {
        #region ISimpleFactory Members

        private const string TypeNameDateTime = "DateTime";

        public bool EncodeSimple(out object value, out string typeName, object instance)
        {
            typeName = TypeNameDateTime;
            if (instance is DateTime when)
            {
                value = Morph.Lib.Conversion.DateTimeToStr(when);
                return true;
            }
            else
            {
                value = null;
                return false;
            }
        }

        public bool DecodeSimple(object value, string typeName, out object instance)
        {
            if ("DateTime".Equals(typeName))
            {
                instance = Morph.Lib.Conversion.StrToDateTime((String)value);
                return true;
            }
            else
            {
                instance = null;
                return false;
            }
        }

        #endregion
    }

    internal class SimpleFactoryNotSupported : ISimpleFactory
    {
        #region ISimpleFactory Members

        public bool EncodeSimple(out object value, out string typeName, object instance)
        {
            value = null;
            typeName = null;
            return false;
        }

        public bool DecodeSimple(object value, string typeName, out object instance)
        {
            if ("Date".Equals(typeName) || "Time".Equals(typeName) || "Currency".Equals(typeName))
                throw new EMorph("The type " + typeName + " is not supported on this implementation.");
            instance = null;
            return false;
        }

        #endregion
    }

    internal class InstanceEncoderException : IInstanceEncoder
    {
        #region IInstanceFactory

        public ValueInstance EncodeInstance(object instance)
        {
            if (!(instance is Exception))
                return null;
            Exception x = (Exception)instance;
            ValueInstance value = new ValueInstance(instance.GetType().Name, true, false);
            value.Struct.Add(x.Message, "message");
            value.Struct.Add(x.StackTrace, "trace");
            return value;
        }

        #endregion
    }

    #endregion

    /// <summary>
    ///  InstanceFactories loops through all factories of a certain type until either:<br/>
    ///  - there are no factories left to try<br/>
    ///  - a factory returns true, meaning that a conversion was successfully dealt with.
    /// </summary>
    public class InstanceFactories
    {
        public InstanceFactories()
        {
            Add(FactoryBool);
            Add(FactoryDateTime);
            Add(FactoryNotSupported);
            Add(EncoderException);
        }

        #region Internal

        #region Predefined types

        //  Very common, so saving memory by instantiating them once and then using for all InstanceFactories
        private static readonly ISimpleFactory FactoryBool = new SimpleFactoryBool();
        private static readonly ISimpleFactory FactoryDateTime = new SimpleFactoryDateTime();
        private static readonly ISimpleFactory FactoryNotSupported = new SimpleFactoryNotSupported();
        private static readonly IInstanceEncoder EncoderException = new InstanceEncoderException();

        #endregion

        private readonly List<IReferenceDecoder> s_referenceDecoders = new List<IReferenceDecoder>();
        private readonly List<IInstanceEncoder> s_instanceEncoders = new List<IInstanceEncoder>();
        private readonly List<IInstanceDecoder> s_instanceDecoders = new List<IInstanceDecoder>();
        private readonly List<ISimpleFactory> s_simpleFactories = new List<ISimpleFactory>();

        internal bool DecodeReference(ServletProxy value, out object reference)
        {
            //  If the ServletProxy has a facade, then use that
            reference = value.Facade;
            if (reference != null)
                return true;
            //  If none found, then create a new one
            for (int i = 0; i < s_referenceDecoders.Count; i++)
                if (s_referenceDecoders[i].DecodeReference(value, out reference))
                {
                    value.Facade = reference;
                    return true;
                }
            //  Oh well, just return the ServletProxy itself then
            reference = value;
            return false;
        }

        internal object EncodeInstance(object instance)
        {
            for (int i = 0; i < s_instanceEncoders.Count; i++)
            {
                ValueInstance result = s_instanceEncoders[i].EncodeInstance(instance);
                //  We know that Instance is not null (because of where the params encoder calls this), so should not be encoded as null
                if (result != null)
                    return result;
            }
            return instance;
        }

        internal bool DecodeInstance(ValueInstance value, out object instance)
        {
            for (int i = 0; i < s_instanceDecoders.Count; i++)
                if (s_instanceDecoders[i].DecodeInstance(value, out instance))
                    return true;
            instance = value;
            return false;
        }

        internal bool EncodeSimple(out object value, out string typeName, object instance)
        {
            for (int i = 0; i < s_simpleFactories.Count; i++)
                if (s_simpleFactories[i].EncodeSimple(out value, out typeName, instance))
                    return true;
            typeName = null;
            value = instance;
            return false;
        }

        internal bool DecodeSimple(object value, string typeName, out object reference)
        {
            if (typeName != null)
                for (int i = 0; i < s_simpleFactories.Count; i++)
                    if (s_simpleFactories[i].DecodeSimple(value, typeName, out reference))
                        return true;
            reference = value;
            return false;
        }

        #endregion

        public void Add(IReferenceDecoder decoder)
        {
            s_referenceDecoders.Add(decoder);
        }

        public void Add(IInstanceEncoder encoder)
        {
            s_instanceEncoders.Add(encoder);
        }

        public void Add(IInstanceDecoder decoder)
        {
            s_instanceDecoders.Add(decoder);
        }

        public void Add(ISimpleFactory factory)
        {
            s_simpleFactories.Add(factory);
        }
    }

    /// <summary>
    ///  <para>Add this to any server side business object to insert the LinkMessage as the 1st parameter.<br/>
    ///  Other than that, the methods are called as usual.</para>
    ///  <para>
    ///  Example:<br/>
    ///   The recieving business object implements IMorphParameters, so...<br/>
    ///   the caller calls:       <c>void MyMethod(string Str, int Num)</c><br/>
    ///   the invoked method is:  <c>void MyMethod(LinkMessage Message, string Str, int Num)</c></para>
    ///  
    ///  <para>Note: This only applies to methods, not properties.</para>
    /// </summary>
    public interface IMorphParameters
    {
    }
}