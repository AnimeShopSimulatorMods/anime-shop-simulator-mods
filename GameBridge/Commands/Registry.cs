using System.Linq;
using GameBridge.Net;

namespace GameBridge.Commands
{
    // Every command the bridge understands, registered in one place so the list is easy to audit.
    internal static class Registry
    {
        public static void RegisterAll(Dispatcher dispatcher)
        {
            dispatcher.Register("ping", _ => new
            {
                bridge = ModInfo.Version,
                commands = dispatcher.Commands.OrderBy(name => name).ToArray(),
            });

            ReflectionCommands.Register(dispatcher);
            PrefsCommands.Register(dispatcher);
            StateCommands.Register(dispatcher);
            CheatCommands.Register(dispatcher);
            WaitCommands.Register(dispatcher);
            VisualCommands.Register(dispatcher);
        }
    }
}
