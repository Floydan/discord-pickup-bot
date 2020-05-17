using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Discord;
using Discord.Addons.Hosting;
using Discord.Addons.Hosting.Reliability;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
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
                .UseSystemd()
                .ConfigureDiscordHost<DiscordSocketClient>((context, configBuilder) =>
                {
                    configBuilder.SetDiscordConfiguration(new DiscordSocketConfig
                    {
                        LogLevel = LogSeverity.Info,
                        AlwaysDownloadUsers = true,
                        MessageCacheSize = 100
                    });
                    configBuilder.SetToken(Environment.GetEnvironmentVariable("DiscordToken"));
                })
                .UseCommandService((context, conf) =>
                {
                    conf.LogLevel = LogSeverity.Warning;
                })
                .ConfigureServices((hostContext, services) =>
                {
                    services
                        .AddHttpClient()
                        .AddSingleton<ITranslationService, GoogleTranslationService>()
                        .AddSingleton<CommandHandlerService>() //added as singleton to be used in event registration below
                        .AddHostedService(p => p.GetService<CommandHandlerService>())
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

                    services.AddLogging(b =>
                    {
                        b.ClearProviders();

                        b.AddConfiguration(hostContext.Configuration.GetSection("Logging"))
                            .AddDebug()
                            .AddEventSourceLogger()
                            .AddConsole();
                    });

                })
                .UseConsoleLifetime();

            using(_host = builder.Build())
            {

                var client = _host.Services.GetRequiredService<DiscordSocketClient>();

                client.JoinedGuild += OnJoinedGuild;
                client.MessageUpdated += OnMessageUpdated;

                var prefix = Environment.GetEnvironmentVariable("CommandPrefix") ?? "!";
                await client.SetActivityAsync(
                    new Game($"Pickup bot | {prefix}help",
                        ActivityType.Playing,
                        ActivityProperties.Play));

                await _host.RunReliablyAsync();
            }
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
            var commandHandler = _host.Services.GetRequiredService<CommandHandlerService>();
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
