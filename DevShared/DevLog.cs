using System;
using MelonLoader;

namespace AnimeShopMods.Dev
{
    // The cheats used to log through CheatForDev.Main, which tied them to one mod. Each host now says
    // where its verbose switch lives, and the cheats ask this class instead.
    internal static class DevLog
    {
        public static Func<bool> VerboseSwitch = () => false;

        public static bool Verbose
        {
            get
            {
                try
                {
                    return VerboseSwitch();
                }
                catch
                {
                    return false;
                }
            }
        }

        public static void Log(string message)
        {
            if (Verbose)
                MelonLogger.Msg(message);
        }
    }
}
