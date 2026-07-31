using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace AsusFanProfileSwitcher.GameBar
{
    internal sealed class GameBarProfile
    {
        public string Name { get; set; }
        public string DisplayName { get; set; }
        public bool IsActive { get; set; }
    }

    internal sealed class GameBarState
    {
        public bool Connected { get; set; }
        public string Status { get; set; }
        public List<GameBarProfile> Profiles { get; set; } = new List<GameBarProfile>();
    }

    internal sealed class GameBarResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public GameBarState State { get; set; }
    }

    internal static class GameBarClient
    {
        private const string PipeName = @"LOCAL\AsusFanProfileSwitcher.GameBar.v1";

        public static Task<GameBarResponse> GetStateAsync()
        {
            return SendAsync(new { command = "state" });
        }

        public static Task<GameBarResponse> ApplyAsync(string profileName)
        {
            return SendAsync(new { command = "apply", profileName });
        }

        private static async Task<GameBarResponse> SendAsync(object request)
        {
            try
            {
                using (var pipe = new NamedPipeClientStream(
                    ".",
                    PipeName,
                    PipeDirection.InOut,
                    PipeOptions.Asynchronous))
                {
                    await pipe.ConnectAsync(1500);
                    using (var writer = new StreamWriter(
                        pipe,
                        new UTF8Encoding(false),
                        4096,
                        true))
                    using (var reader = new StreamReader(
                        pipe,
                        new UTF8Encoding(false),
                        false,
                        4096,
                        true))
                    {
                        writer.AutoFlush = true;
                        await writer.WriteLineAsync(JsonConvert.SerializeObject(request));
                        var payload = await reader.ReadLineAsync();
                        var response = string.IsNullOrWhiteSpace(payload)
                            ? null
                            : JsonConvert.DeserializeObject<GameBarResponse>(payload);
                        return response ?? new GameBarResponse
                        {
                            Success = false,
                            Message = "The desktop controller returned no data."
                        };
                    }
                }
            }
            catch (TimeoutException)
            {
                return Offline();
            }
            catch (IOException)
            {
                return Offline();
            }
            catch (UnauthorizedAccessException)
            {
                return new GameBarResponse
                {
                    Success = false,
                    Message = "Game Bar was not allowed to connect to the controller."
                };
            }
        }

        private static GameBarResponse Offline()
        {
            return new GameBarResponse
            {
                Success = false,
                Message = "Open ASUS Fan Profile Switcher as administrator to use the widget."
            };
        }
    }
}
