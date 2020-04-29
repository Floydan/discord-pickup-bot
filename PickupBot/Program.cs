using System;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
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
            await using var services = ConfigureServices();

            ServiceLocator.SetLocatorProvider(services);
            var client = services.GetRequiredService<DiscordSocketClient>();

            client.Log += LogAsync;
            client.JoinedGuild += OnJoinedGuild;
            client.MessageUpdated += OnMessageUpdated;
            services.GetRequiredService<CommandService>().Log += LogAsync;

            // Tokens should be considered secret data and never hard-coded.
            // We can read from the environment variable to avoid hardcoding.
            await client.LoginAsync(TokenType.Bot, Environment.GetEnvironmentVariable("DiscordToken"));
            await client.StartAsync();

            // Here we initialize the logic required to register our commands.
            await services.GetRequiredService<CommandHandlerService>().InitializeAsync();

            await Task.Delay(-1);
        }

        private static async Task OnJoinedGuild(SocketGuild guild)
        {
            try
            {
                // create #pickup channel if missing
                var channel = guild.Channels.FirstOrDefault(c => c.Name.Equals("pickup"));
                if (channel == null)
                {
                    await guild.CreateTextChannelAsync("pickup",
                        properties => properties.Topic = "powered by pickup-bot | !help for instructions");
                }

                // create applicable roles if missing
                if (guild.Roles.All(w => w.Name != "pickup-promote"))
                    await guild.CreateRoleAsync("pickup-promote", GuildPermissions.None, Color.Orange, isHoisted: false, isMentionable: true);
            }
            catch (Exception e)
            {
                await LogAsync(new LogMessage(LogSeverity.Error, nameof(OnJoinedGuild), e.Message, e));
            }
        }

        private static async Task OnMessageUpdated(Cacheable<IMessage, ulong> before, SocketMessage after, ISocketMessageChannel channel)
        {
            //var message = await before.GetOrDownloadAsync();
            //await LogAsync(new LogMessage(LogSeverity.Info, "OnMessageUpdated", $"{message} -> {after}"));

            var commandHandler = ServiceLocator.Current.GetInstance<CommandHandlerService>();
            if (commandHandler != null)
                await commandHandler.MessageReceivedAsync(after);
        }

        private static Task LogAsync(LogMessage msg)
        {
            Console.WriteLine(msg.ToString());
            return Task.CompletedTask;
        }

        private static ServiceProvider ConfigureServices()
        {
            var storageConnectionString = Environment.GetEnvironmentVariable("StorageConnectionString");

            var discordSocketConfig = new DiscordSocketConfig { MessageCacheSize = 100, LogLevel = LogSeverity.Info };

            return new ServiceCollection()
                .AddSingleton<DiscordSocketClient>(provider => new DiscordSocketClient(discordSocketConfig))
                .AddSingleton<CommandService>()
                .AddSingleton<CommandHandlerService>()
                .AddSingleton<HttpClient>()
                .AddScoped<IAzureTableStorage<PickupQueue>>(provider => 
                    new AzureTableStorage<PickupQueue>(
                        new AzureTableSettings(storageConnectionString, nameof(PickupQueue))
                    )
                )
                .AddScoped<IAzureTableStorage<FlaggedSubscriber>>(provider =>
                    new AzureTableStorage<FlaggedSubscriber>(
                        new AzureTableSettings(storageConnectionString, nameof(FlaggedSubscriber))
                    )
                )
                .AddScoped<IQueueRepository, PickupQueueRepository>()
                .AddScoped<IFlaggedSubscribersRepository, FlaggedSubscribersRepository>()
                .BuildServiceProvider();
        }
    }
}
