using System;
using Morph.Base;
using Morph.Core;
using Morph.Lib;
using Morph.Params;

namespace Morph.Endpoint
{
    public class Replies
    {
        #region Internal

        private readonly RegisterItems<ReplyParams> _replies = new RegisterItems<ReplyParams>();

        private void AddReply(ReplyParams Reply)
        {
            lock (_replies)
                _replies.Add(Reply);
        }

        private class ReplyParams : IRegisterItemID
        {
            public ReplyParams(int id, InstanceFactories instanceFactories, Device device, LinkStack fromPath, LinkData linkData)
              : base()
            {
                _id = id;
                InstanceFactories = instanceFactories;
                Device = device;
                if (fromPath != null)
                    ReverseFromPath = fromPath.Reverse();
                LinkData = linkData;
            }

            private readonly int _id;
            public int ID
            {
                get => _id;
            }

            public InstanceFactories InstanceFactories;

            public Device Device;

            public LinkData LinkData;

            public LinkStack ReverseFromPath = null;
        }

        #endregion

        public void AssignReply(int id, InstanceFactories instanceFactories, Device device, LinkStack fromPath, LinkData linkData)
        {
            AddReply(new ReplyParams(id, instanceFactories, device, fromPath, linkData));
        }

        public object GetReply(int id, out object[] Params, bool hasSpecial)
        {
            //  Extract the Reply
            ReplyParams reply;
            lock (_replies)
            {
                reply = _replies.Find(id);
                _replies.Remove(id);
            }
            //  Examine the reply
            if (reply == null)
                throw new EMorphImplementation();
            if (reply.LinkData == null)
            { //  Nothing returned, that's fine
                Params = null;
                return null;
            }
            //  Decode reply
            object special = null;
            if (hasSpecial)
                Parameters.Decode(reply.InstanceFactories, reply.Device.Path, reply.LinkData.Reader, out Params, out special);
            else
                Parameters.Decode(reply.InstanceFactories, reply.Device.Path, reply.LinkData.Reader, out Params);
            //  Reply might be an exception
            if (reply.LinkData.IsException)
            {
                //  Collect information about the exception
                int errorCode = reply.LinkData.ErrorCode;
                string message = null;
                string trace = reply.ReverseFromPath == null ? "" : reply.ReverseFromPath.ToString();
                if (special != null)
                    if (special is ValueInstance error)
                    {
                        message = (string)error.Struct.ByNameOrNull("message");
                        trace = error.Struct.ByNameOrNull("trace") + trace;
                    }
                    else
                    {
                        //  In this implementation we won't worry about if the return type is wrong.
                    }
                EMorph.Throw(errorCode, message, trace);
            }
            //  Return normally
            return special;
        }

        public void Remove(int id)
        {
            lock (_replies)
                _replies.Remove(id);
        }
    }
}