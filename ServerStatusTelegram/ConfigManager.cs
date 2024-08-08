using Newtonsoft.Json;
using System.Collections.Generic;
using System.IO;
using Formatting = Newtonsoft.Json.Formatting;

namespace ServerStatusTelegram
{
    public class ConfigManager
    {
        private readonly string configFilePath;

        public ConfigManager(string configFilePath)
        {
            this.configFilePath = configFilePath;
        }

        public List<ServerModel> LoadConfig()
        {
            if (!File.Exists(configFilePath))
            {
                CreateDefaultConfig();
            }

            string json = File.ReadAllText(configFilePath);
            List<ServerModel> serverModels = JsonConvert.DeserializeObject<List<ServerModel>>(json);

            return serverModels;
        }

        private void CreateDefaultConfig()
        {
            List<ServerModel> defaultConfig = new List<ServerModel>
            {
                new ServerModel
                {
                    Name = "Server1",
                    Port = "1234",
                    RconPassword = "password1",
                    RconPort = "5678",
                    Token = "6779414621:AAFxR6Uvk05AKHRQrIG9A5P-eED_tZ_bYpI",
                    ChatID = "-1002130052148"
                },
            };

            string json = JsonConvert.SerializeObject(defaultConfig, Formatting.Indented);
            File.WriteAllText(configFilePath, json);
        }
    }
}
