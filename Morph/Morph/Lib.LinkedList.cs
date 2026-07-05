using System;

namespace Morph.Lib.LinkedList
{
    public interface IBookmark
    {
    }

    internal class LinkedListLinkTwoWay<T> : IBookmark
    {
        public LinkedListLinkTwoWay(T data, LinkedListLinkTwoWay<T> linkFalse, LinkedListLinkTwoWay<T> linkTrue)
        {
            Data = data;
            _linkFalse = linkFalse;
            _linkTrue = linkTrue;
        }

        public T Data;

        //  Use two separate fields instead of creating another object, ie. LinkedListLinkTwoWay[]
        private LinkedListLinkTwoWay<T> _linkFalse = null, _linkTrue = null;
        public LinkedListLinkTwoWay<T> this[bool i]
        {
            get => i ? _linkTrue : _linkFalse;
            set
            {
                if (i)
                    _linkTrue = value;
                else
                    _linkFalse = value;
            }
        }
    }

    public class LinkedListTwoWay<T> : IDisposable
    {
        #region IDisposable Members

        private void DisassembleList(LinkedListLinkTwoWay<T> link)
        {
            if (link != null)
            {
                link[false] = null;
                DisassembleList(link[true]);
                link[true] = null;
            }
        }

        public void Dispose()
        {
            DisassembleList(_left);
        }

        #endregion

        private LinkedListLinkTwoWay<T> _left = null;
        private LinkedListLinkTwoWay<T> _right = null;

        public bool HasData
        {
            get => _left != null;
        }

        public IBookmark PushLeft(T data)
        {
            LinkedListLinkTwoWay<T> link = new LinkedListLinkTwoWay<T>(data, null, _left);
            if (_left != null)
                _left[false] = link;
            _left = link;
            if (_right == null)
                _right = link;
            return link;
        }

        public IBookmark PushRight(T data)
        {
            LinkedListLinkTwoWay<T> link = new LinkedListLinkTwoWay<T>(data, _right, null);
            if (_right != null)
                _right[true] = link;
            _right = link;
            if (_left == null)
                _left = link;
            return link;
        }

        public T PeekLeft()
        {
            if (_left == null)
                return default(T);
            return _left.Data;
        }

        public T PeekRight()
        {
            if (_right == null)
                return default(T);
            return _right.Data;
        }

        public T PopLeft()
        {
            if (_left == null)
                return default(T);
            return Pop(_left);
        }

        public T PopRight()
        {
            if (_right == null)
                return default(T);
            return Pop(_right);
        }

        public T Pop(IBookmark bookmark)
        {
            LinkedListLinkTwoWay<T> link = (LinkedListLinkTwoWay<T>)bookmark;
            //  Make neighbours look past Link
            if (link[false] != null)
                link[false][true] = link[true];
            if (link[true] != null)
                link[true][false] = link[false];
            //  Make sure that Left and Right are pointing to the ends
            if (_left == link)
                _left = link[true];
            if (_right == link)
                _right = link[false];
            //  Prevent corruption
            link[false] = null;
            link[true] = null;
            //  Done
            return link.Data;
        }

        public void MoveToLeftEnd(IBookmark bookmark)
        {
            if (_left == bookmark)
                return;
            Pop(bookmark);
            LinkedListLinkTwoWay<T> link = (LinkedListLinkTwoWay<T>)bookmark;
            link[true] = _left;
            if (_left != null)
                _left[false] = link;
            _left = link;
            if (_right == null)
                _right = link;
        }

        public void MoveToRightEnd(IBookmark bookmark)
        {
            if (_right == bookmark)
                return;
            Pop(bookmark);
            LinkedListLinkTwoWay<T> link = (LinkedListLinkTwoWay<T>)bookmark;
            link[false] = _right;
            if (_right != null)
                _right[true] = link;
            _right = link;
            if (_left == null)
                _left = link;
        }
    }
}