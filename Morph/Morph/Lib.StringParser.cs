using System;

namespace Morph.Lib
{
    public class StringParser
    {
        public StringParser(string str)
        {
            _str = str;
            _len = _str.Length;
        }

        private string _str;
        private int _len;
        private int _pos = 0;

        public int Position
        {
            get => _pos;
            set => _pos = value;
        }

        private void Validate()
        {
            if (_pos < 0)
                throw new StringParserException("Position must not be negative.");
            if (_pos >= _len)
                throw new StringParserException("End of string has been reached.");
        }

        public char Current()
        {
            return _str[_pos];
        }

        public string Current(int length)
        {
            Validate();
            return _str.Substring(_pos, length);
        }

        public void Move(int steps)
        {
            Validate();
            _pos += steps;
        }

        public void MoveTo(string subStr, bool absorb)
        {
            Validate();
            int foundPos = _str.IndexOf(subStr, _pos);
            if (foundPos < 0)
                throw new StringParserException("Substring not found: " + subStr);
            _pos = foundPos;
            if (absorb)
                _pos += subStr.Length;
        }

        public string ReadTo(string subStr, bool absorb)
        {
            Validate();
            int oldPos = _pos;
            int foundPos = _str.IndexOf(subStr, oldPos);
            if (foundPos < 0)
                return null;
            _pos = foundPos;
            int subStrLen = _pos - oldPos;
            if (absorb)
                _pos += subStr.Length;
            if (subStrLen > 0)
                return _str.Substring(oldPos, subStrLen);
            return null;
        }

        public string ReadTo(char[] chars, bool absorb)
        {
            Validate();
            int oldPos = _pos;
            //  Find the earliest occurrence of any of the characters
            int newPos = Int32.MaxValue;
            foreach (char c in chars)
            {
                int iPos = _str.IndexOf(c, oldPos);
                if ((iPos >= 0) && (iPos < newPos))
                    newPos = iPos;
            }
            if (newPos == Int32.MaxValue)
                return null;
            _pos = newPos;
            int subStrLen = _pos - oldPos;
            if (absorb)
                _pos++;
            if (subStrLen > 0)
                return _str.Substring(oldPos, subStrLen);
            return null;
        }

        private bool CharInChars(char c, char[] chars)
        {
            foreach (char e in chars)
                if (c == e)
                    return true;
            return false;
        }

        public string ReadChars(char[] chars)
        {
            //  At end-of-string there are simply no characters to read - return null, do not throw
            int oldPos = _pos;
            while ((_pos < _len) && CharInChars(_str[_pos], chars))
                _pos++;
            if (oldPos == _pos)
                return null;
            return _str.Substring(oldPos, _pos - oldPos);
        }

        public bool ReadChar(char Char)
        {
            //  At end-of-string the requested character is not there - return false, do not throw
            if (_pos >= _len)
                return false;
            if (_str[_pos] != Char)
                return false;
            _pos++;
            return true;
        }

        private static readonly char[] Digits = new char[] { '0', '1', '2', '3', '4', '5', '6', '7', '8', '9' };
        public string ReadDigits()
        {
            return ReadChars(Digits);
        }

        public string ReadToEnd()
        {
            return _str.Substring(_pos);
        }

        public bool IsEnded()
        {
            return _pos >= _len;
        }
    }

    public class StringParserException : Exception
    {
        public StringParserException(string message)
          : base(message)
        {
        }
    }
}