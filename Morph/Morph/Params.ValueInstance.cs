using System.Collections.Generic;

namespace Morph.Params
{
    public class ValueInstance : ValueObject
    {
        public ValueInstance(string typeName, bool isStruct, bool isArray)
          : base(typeName)
        {
            if (isArray)
                _array = new ArrayValues();
            if (isStruct)
                _struct = new StructValues();
        }

        private readonly ArrayValues _array;
        public ArrayValues Array
        {
            get => _array;
        }

        private readonly StructValues _struct;
        public StructValues Struct
        {
            get => _struct;
        }
    }

    public class ArrayValues
    {
        private readonly Values _values = new Values();

        public int Count
        {
            get => _values.Count;
        }

        public Values Values
        {
            get => _values;
        }

        public void Add(object value)
        {
            _values.Add(value);
        }
    }

    public class StructValues
    {
        private readonly Values _values = new Values();
        private readonly Names _names = new Names();

        public int Count
        {
            get => _values.Count;
        }

        public Values Values
        {
            get => _values;
        }

        public Names Names
        {
            get => _names;
        }

        public bool HasName(string name)
        {
            return _names.IndexOf(name) >= 0;
        }

        public object ByName(string name)
        {
            int i = _names.IndexOf(name);
            if (i < 0)
                throw new EMorph("Identifier not found: " + name);
            return _values[i];
        }

        public object ByNameOrNull(string name)
        {
            int i = _names.IndexOf(name);
            if (i < 0)
                return null;
            return _values[i];
        }

        public void Add(object value, string name)
        {
            _values.Add(value);
            _names.Add(name);
        }

        public object[] ToObjects()
        {
            if (Count == 0)
                return null;
            return _values._values.ToArray();
        }
    }

    public class Values
    {
        internal List<object> _values = new List<object>();

        internal int Count
        {
            get => _values.Count;
        }

        public object this[int i]
        {
            get => _values[i];
        }

        internal void Add(object Value)
        {
            _values.Add(Value);
        }

        internal object[] ToArray()
        {
            return _values.ToArray();
        }
    }

    public class Names
    {
        private List<string> _names = new List<string>();

        internal int Count
        {
            get => _names.Count;
        }

        public string this[int i]
        {
            get => _names[i];
        }

        internal int IndexOf(string name)
        {
            return _names.IndexOf(name);
        }

        internal void Add(string name)
        {
            _names.Add(name);
        }
    }
}