﻿using System;

namespace Bat.Library.Settings
{
    public class ESettings : Exception
    {
        public ESettings(string message)
          : base(message)
        { }

        internal ESettings(Type dataType)
          : base("No SettingType registered for: " + dataType.ToString())
        { }
    }
}