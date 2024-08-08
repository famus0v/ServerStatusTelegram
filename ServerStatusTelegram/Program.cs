using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ServerStatusTelegram
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            string configFilePath = "config.json";
            ConfigManager configManager = new ConfigManager(configFilePath);

            List<ServerModel> serverModels = configManager.LoadConfig();

            foreach(var serverModel in serverModels)
            {
                _ = new RustServer(serverModel);
            }

            Thread.Sleep(-1);
        }
    }
}
