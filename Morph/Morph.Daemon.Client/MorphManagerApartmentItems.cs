using System;
using Morph.Lib;

namespace Morph.Daemon.Client
{
    internal class MorphManagerApartmentItems : DaemonClient, IIDFactory
    {
        internal MorphManagerApartmentItems(string serviceName, TimeSpan defaultTimeout)
          : base(serviceName, defaultTimeout)
        {
        }

        #region IIDFactory Members

        public int Generate()
        {
            return (int)ServletProxy.CallMethod("Obtain", null, true);
        }

        public void Release(int id)
        {
            ServletProxy.SendMethod("Release", new object[] { id });
        }

        #endregion
    }
}