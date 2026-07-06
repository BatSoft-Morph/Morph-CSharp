/**
 * This is NOT written to be efficient or elegant.
 * This is written to be simple, for a simple demo.
 * So please excuse some poor design decisions.
 */

using System;
using System.Collections;
using System.Collections.Generic;

namespace MorphDemoBookingServer
{
    static public class ObjectInstances
    {
        private static readonly Hashtable s_all = new Hashtable();

        static private ObjectInstance InstanceByName(string objectName)
        {
            return (ObjectInstance)s_all[objectName];
        }

        public static string CurrentOwnerOf(string objectName)
        {
            lock (s_all)
            {
                ObjectInstance instance = InstanceByName(objectName);
                if (instance == null)
                    return null;
                else
                    return instance.CurrentOwner;
            }
        }

        public static string Obtain(string objectName, string clientID)
        {
            lock (s_all)
            {
                ObjectInstance instance = InstanceByName(objectName);
                //  New instance  
                if (instance == null)
                {
                    instance = new ObjectInstance(objectName);
                    s_all.Add(objectName, instance);
                }
                //  Join the queue
                return instance.Request(clientID);
            }
        }

        public static string Release(string objectName, string clientID)
        {
            lock (s_all)
            {
                ObjectInstance instance = InstanceByName(objectName);
                if (instance == null)
                    return null;
                //  Remove client from the queue
                string newOwner = instance.Release(clientID);
                //  If object has no more owners, then tidy it up
                if ((instance != null) && (instance.Count == 0))
                    s_all.Remove(objectName);
                //  Let the caller know who the new owner is
                return newOwner;
            }
        }

        public static void ReleaseAll(string clientID)
        {
            lock (s_all)
                foreach (DictionaryEntry entry in s_all)
                {
                    ObjectInstance instance = (ObjectInstance)(entry.Value);
                    Release(instance.ObjectName, clientID);
                }
        }

        public static string[] ListClientIDs(string objectName)
        {
            lock (s_all)
            {
                ObjectInstance instance = (ObjectInstance)s_all[objectName];
                //  Leave the queue
                if (instance == null)
                    return null;
                else
                    return instance.ToArray();
            }
        }
    }

    public class ObjectInstance : List<string>
    {
        internal ObjectInstance(string objectName)
        {
            _objectName = objectName;
        }

        private readonly string _objectName;
        public string ObjectName
        {
            get { return _objectName; }
        }

        public string CurrentOwner
        {
            get
            {
                if (Count == 0)
                    return null;
                else
                    return this[0];
            }
        }

        internal string Request(string clientID)
        {
            //  Don't do anything if already in the queue
            if (Contains(clientID))
                return CurrentOwner;
            //  Join the queue
            string oldClientID = CurrentOwner;
            Add(clientID);
            string newClientID = CurrentOwner;
            //  Tell the world about the changes
            DoClientIDChanged(oldClientID, newClientID);
            return newClientID;
        }

        internal string Release(string clientID)
        {
            //  Don't do anything if already not in the queue
            if (!Contains(clientID))
                return CurrentOwner;
            //  Leave the queue
            string oldClientID = CurrentOwner;
            Remove(clientID);
            string newClientID = CurrentOwner;
            //  Tell the world about the changes
            DoClientIDChanged(oldClientID, newClientID);
            return newClientID;
        }

        private void DoClientIDChanged(string oldClientID, string newClientID)
        {
            OnClientIDChanged?.Invoke(this, new ClientIDArgs(this, oldClientID, newClientID));
        }

        static public event ClientIDHandler OnClientIDChanged;
    }

    public delegate void ClientIDHandler(object sender, ClientIDArgs e);

    public class ClientIDArgs : EventArgs
    {
        internal ClientIDArgs(ObjectInstance objectInstance, string oldClientID, string newClientID)
          : base()
        {
            _objectInstance = objectInstance;
            _oldClientID = oldClientID;
            _newClientID = newClientID;
        }

        private readonly ObjectInstance _objectInstance;
        public ObjectInstance ObjectInstance
        {
            get { return _objectInstance; }
        }

        private readonly string _oldClientID;
        public string OldClientID
        {
            get { return _oldClientID; }
        }

        private readonly string _newClientID;
        public string NewClientID
        {
            get { return _newClientID; }
        }
    }
}