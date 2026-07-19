using Morph.Base;
using Morph.Core;
using Morph.Params;
using System.Collections.Generic;
using System.Reflection;

namespace Morph.Endpoint
{
    public class LinkMethod : LinkMember
    {
        public LinkMethod(string name)
          : base()
        {
            _name = name;
        }

        private readonly string _name;
        public override string Name
        {
            get => _name;
        }

        #region Link

        public override int Size()
        {
            return 3 + MorphWriter.SizeOfString(Name);
        }

        public override void Write(MorphWriter writer)
        {
            writer.WriteLinkByte(LinkTypeID, false, false, false);
            writer.WriteIdentifier(Name);
        }

        #endregion

        protected internal override LinkData Invoke(LinkMessage message, LinkStack senderDevicePath, LinkData dataIn)
        {
            MorphApartment apartment = _servlet.Apartment;
            //  Obtain the object
            object obj = _servlet.Object;
            //  Obtain the method
            MethodInfo method = obj.GetType().GetMethod(Name);
            if (method == null)
                throw new EMorph("Method not found");
            //  Decode input
            object[] paramsIn = null;
            if (dataIn != null)
                Parameters.Decode(apartment.InstanceFactories, senderDevicePath, dataIn.Reader, out paramsIn);
            //  Might insert Message as the first parameter
            if (obj is IMorphParameters)
            {
                List<object> paramsList = new List<object>() { message };
                if (paramsIn != null)
                    paramsList.AddRange(paramsIn);
                paramsIn = paramsList.ToArray();
            }
            //  Invoke the method
            object result = method.Invoke(obj, paramsIn);
            //  Encode output
            MorphWriter dataOutWriter;
            if (method.ReturnType == typeof(void))
                dataOutWriter = Parameters.Encode(null, apartment.InstanceFactories);
            else
                dataOutWriter = Parameters.Encode(null, result, apartment.InstanceFactories);
            //  Return output
            if (dataOutWriter == null)
                return null;
            else
                return new LinkData(dataOutWriter);
        }

        public override string ToString()
        {
            return "{Method Name=\"" + Name + "\"}";
        }
    }
}