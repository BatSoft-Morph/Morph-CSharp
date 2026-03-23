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
    /** IReferenceDecoder
     * 
     * An IReferenceFactory creates a business object proxy to encapsulate a servlet.
     * 
     * Example:
     *   public class MyInterfaceProxy : MyInterface
     *   {
     *     internal MyInterfaceProxy(ServletProxy ServletProxy)
     *     {
     *       _ServletProxy = ServletProxy;
     *     }
     * 
     *     private ServletProxy _ServletProxy;
     * 
     *     #region MyInterface Members
     * 
     *     public AnyType1 myMethod1(AnyType2 anyParam2, AnyType3 anyParam3)
     *     {
     *       return (AnyType1)_ServletProxy.CallMethod("myMethod1", new object[1] { anyParam2, anyParam3 });
     *     }
     * 
     *     public AnyType3 myProperty1
     *     {
     *       get { return (AnyType3)_ServletProxy.CallGetProperty("myProperty1", null); }
     *     }
     * 
     *     #endregion
     *   }
     */
    public interface IReferenceDecoder
    {
        bool DecodeReference(ServletProxy value, out object reference);
    }

    /** IInstanceEncoder
     * 
     * Tries to encode Instance to ValueInstance.  If not, then returns null.
     * 
     * This is similar to IMorphInstance.  The only difference is that with this
     * the Instance object can be of any type, even a finalised type.
     */
    public interface IInstanceEncoder
    {
        bool EncodeInstance(object value, ref ValueInstance valueInstance, ref string typeName);
    }

    /** IInstanceDecoder
     * 
     * An IInstanceFactory creates an instance based on the supplied Value.
     * 
     * This is really the inverse of IMorphInstance.  A complex object can
     * encode itself using IMorphInstance.MorphEncode(), then on the
     * receiving side, an IInstanceFactory can convert the ValueInstance
     * back to the object as desired.
     * 
     */
    public interface IInstanceDecoder
    {
        bool DecodeInstance(ValueInstance value, out object instance);
    }

    /** ISimpleFactory
     * 
     * A simple factory has the same purpose as IInstanceFactory, except that
     * the Value parameter is limited to the following simple types:
     * - Int8 (Value)
     * - Int16
     * - Int32
     * - Int64
     * - Char
     * - String
     * - array of any of the above types.
     * 
     * For examples, see the implementations of predefined types.
     */
    public interface ISimpleFactory
    {
        bool EncodeSimple(out object value, out string typeName, object instance);
        bool DecodeSimple(object value, string typeName, out object instance);
    }

    #region Useful general implementations

    /**
     * This is meant to be commonly used for handling struct types automatically.
     * Add the struct types using AddStructType() and add to an InstanceFactories.
     * 
     * Note:  This is intended to work only for struct types.  However, it also works
     * for class types that look like structs.  This means having:
     * - a contructor without parameters
     * - fields (InstanceFactoryStruct ignores properties)
     * 
     * Examples:
     *  AddStructType(typeof(MyStruct));
     *  AddStructType(typeof(MyClass));
     */
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

    /**
     * 
     * This is meant to be commonly used for handling array types automatically.
     * Add the array element type using AddArrayElemType() and add to an InstanceFactories.
     * 
     * Examples:
     *  AddArrayElemType(typeof(int));  //  Converts to int[]
     *  AddArrayElemType(typeof(MyClass));  //  Converts to MyClass[]
     */
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

    //internal class SimpleFactoryNotSupported : ISimpleFactory
    //{
    //    #region ISimpleFactory Members

    //    public bool EncodeSimple(out object value, out string typeName, object instance)
    //    {
    //        value = null;
    //        typeName = null;
    //        return false;
    //    }

    //    public bool DecodeSimple(object value, string typeName, out object instance)
    //    {
    //        if ("Date".Equals(typeName) || "Time".Equals(typeName) || "Currency".Equals(typeName))
    //            throw new EMorph("The type " + typeName + " is not supported on this implementation.");
    //        instance = null;
    //        return false;
    //    }

    //    #endregion
    //}

    internal class InstanceEncoderException : IInstanceEncoder
    {
        #region IInstanceFactory

        public bool EncodeInstance(object value, ref ValueInstance valueInstance, ref string typeName)
        {
            if (value is Exception x)
            {
                valueInstance = new ValueInstance(value.GetType().Name, true, false);
                valueInstance.Struct.Add(x.Message, "message");
                valueInstance.Struct.Add(x.StackTrace, "trace");
                return true;
            }
            return false;
        }

        #endregion
    }

    #endregion

    /** InstanceFactories
     * 
     * InstanceFactories loops through all factories of a certain type until either:
     * - there are no factories left to try
     * - a factory returns true, meaning that a conversion was successfully dealt with.
     */
    public class InstanceFactories
    {
        public InstanceFactories()
        {
            //Add(FactoryNotSupported);
            Add(EncoderException);
        }

        #region Internal

        #region Predefined types

        //  Very common, so saving memory by instantiating them once and then using for all InstanceFactories
        //private static readonly ISimpleFactory FactoryNotSupported = new SimpleFactoryNotSupported();
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

        internal bool EncodeInstance(object value, ref ValueInstance result, out string typeName)
        {
            result = null;
            typeName = null;
            //  Ensure we loop through the converters in order, in case order is important
            for (int i = 0; i < s_instanceEncoders.Count; i++)
                if (s_instanceEncoders[i].EncodeInstance(value, ref result, ref typeName))
                    return true;
            return false;
        }

        internal bool DecodeInstance(ValueInstance value, out object instance)
        {
            for (int i = 0; i < s_instanceDecoders.Count; i++)
                if (s_instanceDecoders[i].DecodeInstance(value, out instance))
                    return true;
            instance = value;
            return false;
        }

        internal bool EncodeSimple(out object value, out string typeName, object origValue)
        {
            for (int i = 0; i < s_simpleFactories.Count; i++)
                if (s_simpleFactories[i].EncodeSimple(out value, out typeName, origValue))
                    return true;
            value = origValue;
            typeName = null;
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

    /** IMorphParameters
     * 
     * Add this to any server side business object to insert the LinkMessage as the 1st parameter.
     * Other than that, the methods are called as usual.
     * 
     * Example:
     *  The recieving business object implements IMorphParameters, so...
     *  the caller calls:       void MyMethod(string Str, int Num)
     *  the invoked method is:  void MyMethod(LinkMessage Message, string Str, int Num)
     * 
     * Note: This only applies to methods, not properties.
     */
    public interface IMorphParameters
    {
    }
}