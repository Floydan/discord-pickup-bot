using System;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PickupBot.Commands;
using PickupBot.Data.Models;
using PickupBot.Data.Repositories;

namespace PickupBot
{
    public class Program
    {
        public static void Main(string[] args) => new Program().MainAsync().GetAwaiter().GetResult();

        private async Task MainAsync()
        {
            using (var services = ConfigureServices())
            {
                var client = services.GetRequiredService<DiscordSocketClient>();

                client.Log += LogAsync;
                //client.MessageUpdated += OnMessageUpdated;
                services.GetRequiredService<CommandService>().Log += LogAsync;

                // Tokens should be considered secret data and never hard-coded.
                // We can read from the environment variable to avoid hardcoding.
                await client.LoginAsync(TokenType.Bot, Environment.GetEnvironmentVariable("DiscordToken"));
                await client.StartAsync();

                // Here we initialize the logic required to register our commands.
                await services.GetRequiredService<CommandHandlerService>().InitializeAsync();
                
                await Task.Delay(-1);
            }
        }

        private static async Task OnMessageUpdated(Cacheable<IMessage, ulong> before, SocketMessage after, ISocketMessageChannel channel)
        {
            var message = await before.GetOrDownloadAsync();
            await LogAsync(new LogMessage(LogSeverity.Info, "OnMessageUpdated", $"{message} -> {after}"));

            //TODO: maybe trigger CommandHandlerService to check if the updated message is a command
        }

        private static Task LogAsync(LogMessage msg)
        {
            Console.WriteLine(msg.ToString());
            return Task.CompletedTask;
        }
        
        private static ServiceProvider ConfigureServices()
        {
            var storageConnectionString = Environment.GetEnvironmentVariable("StorageConnectionString");
            return new ServiceCollection()
                .AddSingleton<DiscordSocketClient>()
                .AddSingleton<CommandService>()
                .AddSingleton<CommandHandlerService>()
                .AddSingleton<HttpClient>()
                .AddScoped<IAzureTableStorage<PickupQueue>>(provider => new AzureTableStorage<PickupQueue>(new AzureTableSettings(
                    storageConnectionString,
                    nameof(PickupQueue)
                )))
                .AddScoped<IAzureTableStorage<FlaggedSubscriber>>(provider => new AzureTableStorage<FlaggedSubscriber>(new AzureTableSettings(
                    storageConnectionString,
                    nameof(FlaggedSubscriber)
                )))
                .AddScoped<IQueueRepository, PickupQueueRepository>()
                .AddScoped<IFlaggedSubscribersRepository, FlaggedSubscribersRepository>()
                .BuildServiceProvider();
        }
    }
}
