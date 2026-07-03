using Morph.Endpoint;
using Morph.Params;

namespace Morph.Daemon
{
    public class DaemonFactory : InstanceFactories
    {
        public DaemonFactory()
          : base()
        {
            Add(new CallbackFactory());
        }

        private class CallbackFactory : IReferenceDecoder
        {
            #region IReferenceFactory Members

            public bool DecodeReference(ServletProxy value, out object reference)
            {
                if (!typeof(ServiceCallback).Name.Equals(value.TypeName))
                {
                    reference = null;
                    return false;
                }
                else
                {
                    reference = new ServiceCallback(value);
                    return true;
                }
            }

            #endregion
        }
    }
}