using System;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PickupBot.Commands;
using PickupBot.Data.Models;
using PickupBot.Data.Repositories;
using PickupBot.GitHub;
using PickupBot.Translation.Services;

namespace PickupBot
{
    public class Program
    {
        private static IHost _host;
        private static async Task Main(string[] args)
        {
            var storageConnectionString = Environment.GetEnvironmentVariable("StorageConnectionString");

            var builder = new HostBuilder()
                .ConfigureServices((hostContext, services) =>
                {
                    var discordSocketConfig = new DiscordSocketConfig { MessageCacheSize = 100, LogLevel = LogSeverity.Info };

                    services
                        .AddHttpClient()
                        .AddSingleton<DiscordSocketClient>(provider => new DiscordSocketClient(discordSocketConfig))
                        .AddSingleton<CommandService>()
                        .AddSingleton<ITranslationService, GoogleTranslationService>()
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
                        .AddScoped<IAzureTableStorage<SubscriberActivities>>(provider =>
                            new AzureTableStorage<SubscriberActivities>(
                                new AzureTableSettings(storageConnectionString, nameof(SubscriberActivities))
                            )
                        )
                        .AddScoped<IQueueRepository, PickupQueueRepository>()
                        .AddScoped<IFlaggedSubscribersRepository, FlaggedSubscribersRepository>()
                        .AddScoped<ISubscriberActivitiesRepository, SubscriberActivitiesRepository>();


                    services.AddHttpClient<GitHubService>();

                }).UseConsoleLifetime();

            _host = builder.Build();

            var client = _host.Services.GetRequiredService<DiscordSocketClient>();

            client.Log += LogAsync;
            client.JoinedGuild += OnJoinedGuild;
            client.MessageUpdated += OnMessageUpdated;
            _host.Services.GetRequiredService<CommandService>().Log += LogAsync;

            // Tokens should be considered secret data and never hard-coded.
            // We can read from the environment variable to avoid hardcoding.
            await client.LoginAsync(TokenType.Bot, Environment.GetEnvironmentVariable("DiscordToken"));
            await client.StartAsync();

            // Here we initialize the logic required to register our commands.
            await _host.Services.GetRequiredService<CommandHandlerService>().InitializeAsync();

            //await client.SetGameAsync("Pickup bot", null, ActivityType.Playing);
            var prefix = Environment.GetEnvironmentVariable("CommandPrefix") ?? "!";
            await client.SetActivityAsync(
                new Game($"Pickup bot | {prefix}help",
                    ActivityType.Playing,
                    ActivityProperties.Play));

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

                // create voice channel category if missing
                if (guild.CategoryChannels.FirstOrDefault(c => c.Name.Equals("Pickup voice channels", StringComparison.OrdinalIgnoreCase)) == null)
                    await guild.CreateCategoryChannelAsync("Pickup voice channels");
            }
            catch (Exception e)
            {
                await LogAsync(new LogMessage(LogSeverity.Error, nameof(OnJoinedGuild), e.Message, e));
            }
        }

        private static async Task OnMessageUpdated(Cacheable<IMessage, ulong> before, SocketMessage after, ISocketMessageChannel channel)
        {
            var commandHandler = _host.Services.GetService<CommandHandlerService>();
            if (commandHandler != null)
                await commandHandler.MessageReceivedAsync(after);
        }

        private static Task LogAsync(LogMessage msg)
        {
            Console.WriteLine(msg.ToString());
            return Task.CompletedTask;
        }
    }
}
