using System.Collections;
using System.Collections.Generic;

namespace Morph.Lib
{
    public interface IRegisterItemID
    {
        int ID
        {
            get;
        }
    }

    public interface IRegisterItemName
    {
        string Name
        {
            get;
        }
    }

    public class RegisterItems<T> : IEnumerable
    {
        #region Private

        private readonly Hashtable _items = new Hashtable();

        private void Add(object key, T item)
        {
            if (!_items.Contains(key))
                _items.Add(key, item);
        }

        #endregion

        #region Public

        public int Count
        {
            get
            {
                lock (_items)
                    return _items.Count;
            }
        }

        public void Add(T item)
        {
            lock (_items)
            {
                if (item is IRegisterItemID itemID)
                    Add(itemID.ID, item);
                if (item is IRegisterItemName itemName)
                    Add(itemName.Name, item);
            }
        }

        public void Remove(T item)
        {
            lock (_items)
            {
                if (item is IRegisterItemID itemID)
                    _items.Remove(itemID.ID);
                if (item is IRegisterItemName itemName)
                    _items.Remove(itemName.Name);
            }
        }

        public void Remove(object key)
        {
            T item = Find(key);
            if (item != null)
                Remove(item);
        }

        public virtual T Find(object key)
        {
            lock (_items)
                return (T)_items[key];
        }

        public List<T> List()
        {
            List<T> Result = new List<T>();
            lock (_items)
            {
                IEnumerator enums = _items.GetEnumerator();
                while (enums.MoveNext())
                    Result.Add((T)((DictionaryEntry)enums.Current).Value);
            }
            return Result;
        }

        #endregion

        #region IEnumerable Members

        public IEnumerator GetEnumerator()
        {
            return _items.GetEnumerator();
        }

        #endregion
    }
}