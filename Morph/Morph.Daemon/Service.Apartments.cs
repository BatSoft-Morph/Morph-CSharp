using Morph.Base;
using Morph.Internet;
using Morph.Lib;
using Morph.Params;

namespace Morph.Daemon
{
    public class ApartmentObjects : MorphReference, IMorphParameters
    {
        internal ApartmentObjects(string typeName, IIDFactory idFactory, RegisteredApartments registered)
          : base(typeName)
        {
            _idFactory = idFactory;
            _registered = registered;
        }

        private readonly IIDFactory _idFactory;
        private readonly RegisteredApartments _registered;

        public int Obtain(LinkMessage message)
        {
            int ID = _idFactory.Generate();
            if (message is LinkMessageFromIP)
                new RegisteredApartmentInternet(_registered, ID, ((LinkMessageFromIP)message).Connection);
            else
                throw new EMorphDaemon(GetType().Name + ".Obtain(): Unhandled message type \"" + message.GetType().Name + "\".");
            return ID;
        }

        public void Release(LinkMessage message, int id)
        {
            if (message is LinkMessageFromIP)
                _registered.Unregister(id);
            else
                throw new EMorphDaemon(GetType().Name + ".Release(): Unhandled message type \"" + message.GetType().Name + "\".");
        }
    }
}