/* Client side usage
 * 
 * MorphApartmentProxy MorphService = MorphApartmentProxy.ViaString("test.com", "Test", 600000);
 * ServletProxy Servlet = MorphService.DefaultServlet;
 * object result = Servlet.CallMethod("Method", InParams, out OutParams);
 */

using Morph.Base;
using Morph.Core;
using Morph.Lib;
using Morph.Params;

namespace Morph.Endpoint
{
    public class ServletProxy : IRegisterItemID
    {
        internal ServletProxy(MorphApartmentProxy apartmentProxy, int ID, string typeName)
        {
            _apartmentProxy = apartmentProxy;
            _id = ID;
            _typeName = typeName;
        }

        private readonly MorphApartmentProxy _apartmentProxy;
        public MorphApartmentProxy ApartmentProxy
        {
            get => _apartmentProxy;
        }

        #region RegisterItemID Members

        private readonly int _id;
        public int ID
        {
            get => _id;
        }

        #endregion

        #region Private

        private LinkData ParamsToLink(object special, object[] Params)
        {
            if (special != null)
                return new LinkData(Parameters.Encode(Params, special, _apartmentProxy.InstanceFactories));
            if (Params != null)
                return new LinkData(Parameters.Encode(Params, _apartmentProxy.InstanceFactories));
            return null;
        }

        private void Send(LinkMember member, object special, object[] inParams)
        {
            LinkMessage message = new LinkMessage(new LinkStack(), null, false);
            //  Params
            message.PathTo.Push(ParamsToLink(special, inParams));
            //  Method
            message.PathTo.Push(member);
            //  Servlet
            message.PathTo.Push(new LinkServlet(ID));
            //  Sequence
            _apartmentProxy._sequenceSender?.AddNextLink(false, message);
            //  Request
            _apartmentProxy.Send(message);
        }

        private object Call(LinkMember member, object special, object[] inParams, out object[] outParams, bool hasReturnValue)
        {
            //  Determine if we need a path to the apartment in the reply
            LinkStack fromPath = null;
            if (_apartmentProxy.RequiresFromPath)
                fromPath = new LinkStack();
            //  Create the message
            LinkMessage Message = new LinkMessage(new LinkStack(), fromPath, true);
            //  Params
            Message.PathTo.Push(ParamsToLink(special, inParams));
            //  Method
            Message.PathTo.Push(member);
            //  Servlet
            Message.PathTo.Push(new LinkServlet(ID));
            //  Sequence
            _apartmentProxy._sequenceSender?.AddNextLink(false, Message);
            //  Request/Response
            return _apartmentProxy.Call(Message, out outParams, hasReturnValue);
        }

        #endregion

        #region Public

        private readonly string _typeName = null;
        public string TypeName
        {
            get => _typeName;
        }

        public object Facade = null;

        public LinkStack Path
        {
            get
            {
                LinkStack result = new LinkStack();
                result.Append(_apartmentProxy.Path);
                result.Append(new LinkServlet(_id));
                return result;
            }
        }

        public void SendMethod(string methodName, object[] inParams)
        {
            Send(new LinkMethod(methodName), null, inParams);
        }

        public object CallMethod(string methodName, object[] inParams, out object[] outParams, bool hasReturnValue)
        {
            return Call(new LinkMethod(methodName), null, inParams, out outParams, hasReturnValue);
        }

        public object CallMethod(string methodName, object[] inParams, bool hasReturnValue)
        {
            return CallMethod(methodName, inParams, out _, hasReturnValue);
        }

        public void SendSetProperty(string propertyName, object value, object[] index)
        {
            Send(new LinkProperty(propertyName, true, index != null), value, index);
        }

        public void CallSetProperty(string propertyName, object value, object[] index)
        {
            Call(new LinkProperty(propertyName, true, index != null), value, index, out _, false);
        }

        public object CallGetProperty(string propertyName, object[] index)
        {
            return Call(new LinkProperty(propertyName, false, index != null), null, index, out _, true);
        }

        #endregion
    }

    internal class ServletProxies
    {
        internal ServletProxies(MorphApartmentProxy apartmentProxy)
        {
            _apartmentProxy = apartmentProxy;
        }

        private readonly MorphApartmentProxy _apartmentProxy;
        private readonly RegisterItems<ServletProxy> _servletProxies = new RegisterItems<ServletProxy>();

        public ServletProxy Obtain(int id, string typeName)
        {
            ServletProxy result;
            lock (_servletProxies)
            {
                result = _servletProxies.Find(id);
                if (result == null)
                {
                    result = new ServletProxy(_apartmentProxy, id, typeName);
                    _servletProxies.Add(result);
                }
            }
            return result;
        }

        public ServletProxy Find(int id)
        {
            lock (_servletProxies)
                return _servletProxies.Find(id);
        }
    }

    #region Useful general implementation

    public class ObjectProxy
    {
        protected ObjectProxy(ServletProxy proxy)
          : base()
        {
            _proxy = proxy;
        }

        protected ServletProxy _proxy;
    }

    #endregion
}