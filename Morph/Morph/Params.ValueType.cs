namespace Morph.Params
{
    /// <summary>
    /// <para>The ValueType byte that begins every encoded value.</para>
    /// <para>Layout per "Morph Protocol.xlsx" tab "ValueType".</para>
    /// </summary>
    public static class ValueType
    {
        #region Applies to every value

        public const byte HasTypeName = 0x01;
        public const byte HasValueName = 0x02;
        /// <summary>Reserved - not to be used yet.  If set, this is only the type information, no value data.</summary>
        public const byte IsTypeDefinition = 0x04;
        /// <summary>Unset means the value is sent by value.  Iff set and not IsNull, a 4 byte ReferenceID follows the names.</summary>
        public const byte IsReference = 0x08;
        /// <summary>Iff set, nothing follows the names, whatever the other flags say.
        /// Only meaningful when encoding a value.  When decoding a type, ignore this flag.</summary>
        public const byte IsNull = 0x80;

        #endregion

        #region By value:  bits 4-5 select the kind of value

        public const byte ValueKindMask = 0x30;
        public const byte IsSimple = 0x00;
        public const byte IsArray = 0x10;
        public const byte IsStruct = 0x20;
        public const byte IsCustom = 0x30;

        //  Array
        /// <summary>Iff set, all elements are of the one type declared up front, and elements are encoded without their own ValueTypes.</summary>
        public const byte IsArrayElemType = 0x40;

        #endregion

        #region By reference:  bit 4 selects the kind of reference

        /// <summary>Unset means a stream reference.</summary>
        public const byte IsServlet = 0x10;

        //  Servlet
        public const byte HasDevicePath = 0x20;
        public const byte HasArrayIndex = 0x40;

        #endregion
    }
}
