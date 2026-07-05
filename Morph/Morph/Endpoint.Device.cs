using System;
using System.Collections;
using System.Collections.Generic;
using Morph.Core;
using Morph.Params;

namespace Morph.Endpoint
{
    public class Device
    {
        internal Device(LinkStack path)
        {
            _path = path;
        }

        internal Hashtable _apartmentProxiesByApartmentID = new Hashtable();

        internal LinkStack _path;
        public LinkStack Path
        {
            get => _path;
        }

        public TimeSpan DefaultTimeout = new TimeSpan(0, 1, 0);

        public MorphApartmentProxy Find(int apartmentID)
        {
            lock (_apartmentProxiesByApartmentID)
                return (MorphApartmentProxy)_apartmentProxiesByApartmentID[apartmentID];
        }

        public MorphApartmentProxy Obtain(int apartmentID, InstanceFactories instanceFactories)
        {
            lock (_apartmentProxiesByApartmentID)
            {
                MorphApartmentProxy result = Find(apartmentID);
                if (result == null)
                    result = new MorphApartmentProxy(this, apartmentID, DefaultTimeout, instanceFactories);
                return result;
            }
        }
    }

    public class Devices
    {
        private static readonly List<Device> s_all = new List<Device>();

        static public Device Find(LinkStack path)
        {
            lock (s_all)
                for (int i = s_all.Count - 1; i >= 0; i--)
                    if (path.Equals(s_all[i].Path))
                        return s_all[i];
            return null;
        }

        static public Device Obtain(LinkStack path)
        {
            lock (s_all)
            {
                Device result = Find(path);
                if (result == null)
                {
                    result = new Device(path);
                    s_all.Add(result);
                }
                return result;
            }
        }
    }
}