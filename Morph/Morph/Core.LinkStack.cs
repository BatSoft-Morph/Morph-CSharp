using System.Collections.Generic;

namespace Morph.Core
{
    public class LinkStack
    {
        public LinkStack()
        {
            _links = new List<Link>();
            _reader = null;
        }

        public LinkStack(MorphReader reader)
        {
            _links = new List<Link>();
            _reader = reader;
        }

        public LinkStack(byte[] bytes)
        {
            _links = new List<Link>();
            _reader = new MorphReaderSized(bytes);
        }

        public LinkStack(List<Link> links)
        {
            _links = links;
            _reader = null;
        }

        private readonly List<Link> _links;
        private readonly MorphReader _reader;

        private List<Link> CloneList(List<Link> sourceList)
        {
            List<Link> result = new List<Link>();
            for (int i = 0; i < sourceList.Count; i++)
                result.Add(sourceList[i]);
            return result;
        }

        public void PeekAll()
        {
            if (_reader != null)
                while (_reader.CanRead)
                    _links.Insert(0, LinkTypes.ReadLink(_reader));
        }

        public Link Peek()
        {
            if (_links.Count == 0)
            { //  Read next link
                if (_reader == null)
                    return null;
                Link link = LinkTypes.ReadLink(_reader);
                if (link == null)
                    return null;
                //  Add it
                _links.Add(link);
            }
            //  Return the top of the stack
            return _links[_links.Count - 1];
        }

        public Link Pop()
        {
            Link Link = Peek();
            if (_links.Count > 0)
                _links.RemoveAt(_links.Count - 1);
            return Link;
        }

        public void Push(Link link)
        {
            if (link != null)
                _links.Add(link);
        }

        public void Push(LinkStack stack)
        {
            if (stack == null)
                return;
            //  Decode all the links
            stack.PeekAll();
            //  Add the links to Stack
            for (int i = 0; i < stack._links.Count; i++)
                _links.Add(stack._links[i]);
        }

        public void Append(Link link)
        {
            if (link == null)
                return;
            //  Decode all the links
            PeekAll();
            //  Add the link to Stack
            if (link != null)
                _links.Insert(0, link);
        }

        public void Append(LinkStack stack)
        {
            //  Decode all the links
            stack.PeekAll();
            //  Add the links to Stack
            for (int i = 0; i < stack._links.Count; i++)
                _links.Insert(i, stack._links[i]);
        }

        public int ByteSize
        {
            get
            {
                int result = 0;
                for (int i = _links.Count - 1; 0 <= i; i--)
                {
                    int size = _links[i].Size();
                    result += size;
                }
                if (_reader != null)
                    result += ((int)_reader.Remaining);
                return result;
            }
        }

        public void Write(MorphWriter writer)
        {
            for (int i = _links.Count - 1; 0 <= i; i--)
                _links[i].Write(writer);
            if (_reader != null)
                writer.WriteStream(_reader);
        }

        public List<Link> ToLinks()
        {
            PeekAll();
            return CloneList(_links);
        }

        public LinkStack Clone()
        {
            PeekAll();
            return new LinkStack(CloneList(_links));
        }

        public LinkStack Reverse()
        {
            List<Link> links = ToLinks();
            List<Link> reverse = new List<Link>();
            for (int i = links.Count - 1; i >= 0; i--)
                reverse.Add(links[i]);
            return new LinkStack(reverse);
        }

        #region Object overrides

        public override bool Equals(object obj)
        {
            if (!(obj is LinkStack))
                return false;
            LinkStack other = (LinkStack)obj;
            //  Convert binary to objects
            this.PeekAll();
            other.PeekAll();
            //  Compare the objects that make up the paths
            if (this._links.Count != other._links.Count)
                return false;
            for (int i = _links.Count - 1; i >= 0; i--)
                if (!_links[i].Equals(other._links[i]))
                    return false;
            return true;
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }

        public override string ToString()
        {
            /*
            PeekAll();
            string str = null;
            for (int i = _Links.Count - 1; 0 <= i; i--)
              str += "\r\n" + _Links[i].ToString();
            return str;
             * */
            string str = "(";
            for (int i = _links.Count - 1; 0 <= i; i--)
                str += _links[i].ToString();
            if ((_reader != null) && (_reader.CanRead))
                str += "...[" + _reader.Remaining.ToString() + " B]";
            return str + ')';
        }

        #endregion
    }
}