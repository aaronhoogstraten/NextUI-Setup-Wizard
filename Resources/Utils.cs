using System;

namespace NextUI_Setup_Wizard.Resources
{
    public enum OSType
    {
        Windows,
        Mac,
        Unsupported
    }

    public static class Utils
    {
        public static OSType CurrentOS
        {
            get
            {
#if WINDOWS
                return OSType.Windows;
#elif MACCATALYST
                return OSType.Mac;
#else
                return OSType.Unsupported;
#endif
            }
        }
    }
}