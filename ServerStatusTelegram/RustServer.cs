using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ServerStatusTelegram
{
    public class RustServer
    {
        private readonly ServerModel _serverModel;
        private bool IsTestingNow = false;
        private System.Timers.Timer timer;

        public RustServer(ServerModel serverModel)
        {
            _serverModel = serverModel;
            Console.WriteLine($"{DateTime.Now}: Инициализирован сервер {serverModel.Name}");
            SendTelegramMessage(serverModel.Token, serverModel.ChatID, $"{DateTime.Now}: Инициализирован сервер {serverModel.Name}");
            InitializeTimer();
        }

        private void InitializeTimer()
        {
            timer = new System.Timers.Timer();
            timer.Interval = TimeSpan.FromMinutes(1).TotalMilliseconds;
            timer.Elapsed += async (sender, e) => await CheckAndRestart();
            timer.Start();
        }

        public async Task CheckAndRestart()
        {
            var serverName = _serverModel.Name;

            if (IsTestingNow)
            {
                Console.WriteLine($"{DateTime.Now}: Сервер {serverName} в процессе проверки. Пропускаем проверку.");
                return;
            }

            IsTestingNow = true;

            Console.WriteLine($"{DateTime.Now}: Проверяем сервер {serverName} на доступность {_serverModel.IP}:{_serverModel.RconPort}");
            var response = await CheckServerStatusAsync(_serverModel.IP, int.Parse(_serverModel.RconPort), _serverModel.RconPassword);

            for (int i = 0; i < 3; i++)
            {
                if (response == true)
                {
                    Console.WriteLine($"{DateTime.Now}: Сервер {serverName} работает");
                    IsTestingNow = false;
                    return;
                }

                Console.WriteLine($"{DateTime.Now}: Сервер {serverName} НЕ работает");
                response = await CheckServerStatusAsync(_serverModel.IP, int.Parse(_serverModel.RconPort), _serverModel.RconPassword);
                await Task.Delay(5000);
            }

            Console.WriteLine($"{DateTime.Now}: Сервер {serverName} висит");
            await SendTelegramMessage(_serverModel.Token, _serverModel.ChatID, $"{DateTime.Now}: Сервер {serverName} висит");

            IsTestingNow = false;
        }

        private async Task SendTelegramMessage(string botToken, string chatId, string message)
        {
            //return;
            string url = $"https://api.telegram.org/bot{botToken}/sendMessage";
            var payload = new
            {
                chat_id = chatId,
                text = message
            };

            try
            {
                using (var client = new HttpClient())
                {
                    var jsonPayload = Newtonsoft.Json.JsonConvert.SerializeObject(payload);
                    var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

                    var response = await client.PostAsync(url, content);

                    if (response.IsSuccessStatusCode)
                    {
                        Console.WriteLine("Сообщение успешно отправлено.");
                    }
                    else
                    {
                        var errorMessage = await response.Content.ReadAsStringAsync();
                        Console.WriteLine($"Не удалось отправить сообщение: {errorMessage}");
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"Произошла ошибка: {e.Message}");
            }
        }

        public async Task<bool> CheckServerStatusAsync(string host, int port, string password)
        {
            try
            {
                var responseTask = SendRconCommandAsync(host, port, password, "status");
                var timeoutTask = Task.Delay(30000); // Таймаут в 30 секунд

                var completedTask = await Task.WhenAny(responseTask, timeoutTask);

                if (completedTask == responseTask)
                {
                    var response = await responseTask;

                    if (response != null)
                    {
                        var isAlive = response.RootElement.TryGetProperty("Message", out var messageProperty) &&
                           messageProperty.ValueKind == JsonValueKind.String &&
                           messageProperty.GetString()?.Contains("hostname") == true;

                        Console.WriteLine($"{DateTime.Now}: Статус сервера проверен: {isAlive}");
                        return isAlive;
                    }
                    else
                    {
                        Console.WriteLine($"{DateTime.Now}: Ответ RCON команды пуст (null)");
                        return false;
                    }
                }
                else
                {
                    Console.WriteLine($"{DateTime.Now}: Время ожидания истекло");
                    return false;
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"{DateTime.Now}: Ошибка при проверке статуса сервера: {e}");
                return false;
            }
        }

        public async Task<JsonDocument> SendRconCommandAsync(string host, int port, string password, string command)
        {
            JsonDocument parsedResponse = null;
            string wsUrl = $"ws://{host}:{port}/{password}";

            Console.WriteLine($"{DateTime.Now}: Отправляем RCON команду {command} на {host}:{port}");

            try
            {
                using (ClientWebSocket ws = new ClientWebSocket())
                {
                    CancellationTokenSource cts = new CancellationTokenSource();
                    cts.CancelAfter(30000); // Таймаут в 30 секунд

                    await ws.ConnectAsync(new Uri(wsUrl), cts.Token);

                    string payload = JsonSerializer.Serialize(new
                    {
                        Message = command,
                        Identifier = 1,
                        Name = "MyServer"
                    });

                    ArraySegment<byte> payloadBytes = new ArraySegment<byte>(Encoding.UTF8.GetBytes(payload));

                    await ws.SendAsync(payloadBytes, WebSocketMessageType.Text, true, cts.Token);

                    List<byte> responseData = new List<byte>();
                    ArraySegment<byte> responseBytes = new ArraySegment<byte>(new byte[4096]);
                    WebSocketReceiveResult receiveResult;

                    do
                    {
                        receiveResult = await ws.ReceiveAsync(responseBytes, cts.Token);
                        responseData.AddRange(responseBytes.Array.Take(receiveResult.Count));

                    } while (!receiveResult.EndOfMessage);

                    //WebSocketReceiveResult receiveResult = await ws.ReceiveAsync(responseBytes, cts.Token);

                    byte[] responseArray = responseData.ToArray();
                    string response = Encoding.UTF8.GetString(responseArray, 0, responseArray.Length);
                    parsedResponse = JsonDocument.Parse(response);

                    Console.WriteLine($"{DateTime.Now}: Ответ сервера: {parsedResponse}");
                }
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine($"{DateTime.Now}: Время ожидания истекло");
            }
            catch (Exception e)
            {
                Console.WriteLine($"{DateTime.Now}: Ошибка при отправке RCON команды: {e}");
            }

            return parsedResponse;
        }
    }


}
