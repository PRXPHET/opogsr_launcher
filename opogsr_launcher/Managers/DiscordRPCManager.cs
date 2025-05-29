using DiscordRPC;
using opogsr_launcher.Properties;

namespace opogsr_launcher.Managers
{
    public class DiscordRPCManager
    {
        public DiscordRpcClient Client = new("1206867673816760350");

        public DiscordRPCManager()
        {
            Client.Logger = new DiscordRPC.Logging.ConsoleLogger(DiscordRPC.Logging.LogLevel.Trace, true);

            Client.Initialize();

            Client.SetPresence(new RichPresence()
            {
                Details = Resources.Launcher,
                State = Resources.StateIdling,
                Assets = new Assets()
                {
                    LargeImageKey = "main_image"
                },
                Timestamps = Timestamps.Now
            });

            Client.Invoke();
        }
    }
}
