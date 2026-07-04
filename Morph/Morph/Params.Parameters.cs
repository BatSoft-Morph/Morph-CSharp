using Morph.Core;
using System;
using System.IO;

namespace Morph.Params
{
    /// <summary>
    /// <para>Encodes and decodes the Data of a Data link:  ParamCount followed by the parameters.</para>
    /// <para>Per "Morph Protocol.xlsx" tab "LinkType Data":
    /// the Special is always the last parameter and is included in ParamCount;
    /// it is encoded no differently from any other parameter;
    /// when there are no parameters at all, the Data link is omitted entirely.</para>
    /// </summary>
    public static class Parameters
    {
        #region Encoding

        static public MorphWriter Encode(object[] Params, InstanceFactories instanceFactories)
        {
            if ((Params == null) || (Params.Length == 0))
                return null;    //  No parameters:  the Data link is omitted entirely
            MorphWriter writer = new MorphWriter(new MemoryStream());
            //  ParamCount
            writer.WriteInt32(Params.Length);
            //  Parameters
            foreach (object param in Params)
                ValueCodec.EncodeValue(writer, instanceFactories, null, param);
            return writer;
        }

        static public MorphWriter Encode(object[] Params, object special, InstanceFactories instanceFactories)
        {
            //  The Special is always the last parameter
            object[] allParams = new object[(Params == null ? 0 : Params.Length) + 1];
            if (Params != null)
                Array.Copy(Params, allParams, Params.Length);
            allParams[allParams.Length - 1] = special;
            return Encode(allParams, instanceFactories);
        }

        #endregion

        #region Decoding

        static public void Decode(InstanceFactories instanceFactories, LinkStack devicePath, MorphReader dataReader, out object[] Params)
        {
            Params = null;
            if ((dataReader == null) || !dataReader.CanRead)
                return;
            //  ParamCount
            int paramCount = dataReader.ReadInt32();
            //  Parameters
            if (paramCount > 0)
            {
                Params = new object[paramCount];
                for (int i = 0; i < paramCount; i++)
                    Params[i] = ValueCodec.DecodeValue(dataReader, instanceFactories, devicePath);
            }
        }

        static public void Decode(InstanceFactories instanceFactories, LinkStack devicePath, MorphReader dataReader, out object[] Params, out object special)
        {
            Params = null;
            special = null;
            if ((dataReader == null) || !dataReader.CanRead)
                return;
            //  ParamCount includes the Special, which is the last parameter
            int paramCount = dataReader.ReadInt32() - 1;
            if (paramCount < 0)
                return;
            //  Parameters
            if (paramCount > 0)
            {
                Params = new object[paramCount];
                for (int i = 0; i < paramCount; i++)
                    Params[i] = ValueCodec.DecodeValue(dataReader, instanceFactories, devicePath);
            }
            //  Special
            special = ValueCodec.DecodeValue(dataReader, instanceFactories, devicePath);
        }

        #endregion
    }
}
