using System.Reflection;
using Morph.Base;
using Morph.Core;
using Morph.Params;

namespace Morph.Endpoint
{
    public class LinkProperty : LinkMember
    {
        public LinkProperty(string name, bool isSet, bool hasIndex)
          : base()
        {
            _name = name;
            _isSet = isSet;
            _hasIndex = hasIndex;
        }

        private readonly string _name;
        public override string Name
        {
            get => _name;
        }

        private readonly bool _isSet;
        public bool IsSet
        {
            get => _isSet;
        }

        private readonly bool _hasIndex;
        public bool HasIndex
        {
            get => _hasIndex;
        }

        #region Link

        public override int Size()
        {
            return 5 + MorphWriter.SizeOfString(_name);
        }

        public override void Write(MorphWriter writer)
        {
            writer.WriteLinkByte(LinkTypeID, true, _isSet, _hasIndex);
            writer.WriteString(_name);
        }

        #endregion

        protected internal override LinkData Invoke(LinkMessage message, LinkStack senderDevicePath, LinkData dataIn)
        {
            MorphApartment apartment = _servlet.Apartment; ;
            //  Obtain the object
            object obj = _servlet.Object;
            //  Obtain the property
            PropertyInfo property = obj.GetType().GetProperty(Name);
            if (property == null)
                if (_isSet)
                    throw new EMorph("Property setter not found");
                else
                    throw new EMorph("Property getter not found");
            if (dataIn == null && IsSet)
                throw new EMorphInvocation(property.Name, "Tried to set a property value without a value: " + property.Name, null);
            //  Invoke the property
            if (IsSet)
            {
                //  Decode input
                Parameters.Decode(apartment.InstanceFactories, senderDevicePath, dataIn.Reader, out object[] index, out object value);
                //  Invoke the property
                property.SetValue(obj, value, index);
                return null;
            }
            else
            {
                //  Decode input
                Parameters.Decode(apartment.InstanceFactories, senderDevicePath, dataIn.Reader, out object[] index);
                //  Invoke the property
                object value = property.GetValue(obj, index);
                //  Encode output
                return new LinkData(Parameters.Encode(null, value, apartment.InstanceFactories));
            }
        }

        public override string ToString()
        {
            string result = "{Property ";
            if (IsSet)
                result += "Set ";
            else
                result += "Get ";
            result += "Name=\"" + Name + "\"";
            if (HasIndex)
                result += "[]";
            return result + "}"; ;
        }
    }
}