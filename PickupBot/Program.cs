﻿using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Discord;
using Discord.Addons.Hosting;
using Discord.Addons.Hosting.Reliability;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Hosting.Internal;
using Microsoft.Extensions.Logging;
using PickupBot.Commands;
using PickupBot.Commands.Extensions;
using PickupBot.Data.Models;
using PickupBot.Data.Repositories;
using PickupBot.GitHub;
using PickupBot.Infrastructure;
using PickupBot.Translation.Services;

namespace PickupBot
{
    public class Program
    {
        private static IServiceProvider _serviceProvider;

        private static async Task<int> Main(string[] args)
        {
            var environment = Environments.Development;
            if (!args.IsNullOrEmpty())
            {
                environment = args.FirstOrDefault();
            }

            var builder = new HostBuilder()
                .UseSystemd()
                .UseEnvironment(environment)
                .ConfigureHostConfiguration(configHost =>
                {
                    configHost.AddEnvironmentVariables(prefix: "ASPNETCORE_");
                    configHost.AddCommandLine(args);
                })
                .ConfigureAppConfiguration((hostContext, configBuilder) =>
                    {
                        configBuilder
                            .AddEnvironmentVariables(prefix: "ASPNETCORE_")
                            .SetBasePath(Path.GetDirectoryName(hostContext.HostingEnvironment.ContentRootPath))
                            .AddJsonFile("appSettings.json", optional: false, reloadOnChange: true)
                            .AddJsonFile($"appSettings.{hostContext.HostingEnvironment.EnvironmentName}.json", false);
                    })
                .ConfigureDiscordHost<DiscordSocketClient>((context, configBuilder) =>
                {
                    configBuilder.SetDiscordConfiguration(new DiscordSocketConfig
                    {
                        LogLevel = LogSeverity.Info,
                        AlwaysDownloadUsers = true,
                        MessageCacheSize = 100
                    });
                    configBuilder.SetToken(context.Configuration.GetSection("PickupBot:DiscordToken").Value);
                })
                .UseCommandService((context, conf) =>
                {
                    conf.LogLevel = LogSeverity.Warning;
                })
                .ConfigureServices((hostContext, services) =>
                {
                    var storageConnectionString =
                        hostContext.Configuration.GetConnectionString("StorageConnectionString");

                    services
                        .AddHttpClient()
                        .ConfigureSettings<PickupBotSettings>(hostContext.Configuration.GetSection("PickupBot"))
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
                            .AddEventSourceLogger()
                            .AddConsole();

                        if (hostContext.HostingEnvironment.IsDevelopment())
                        {
                            b.AddDebug();
                        }
                    });

                })
                .UseConsoleLifetime();

            using (var host = builder.Build())
            {
                _serviceProvider = host.Services;
                using var scope = _serviceProvider.CreateScope();
                var client = scope.ServiceProvider.GetRequiredService<DiscordSocketClient>();
                var pickupBotSettings = scope.ServiceProvider.GetRequiredService<PickupBotSettings>();

                client.JoinedGuild += OnJoinedGuild;
                client.MessageUpdated += OnMessageUpdated;

                var prefix = pickupBotSettings.CommandPrefix ?? "!";
                await client.SetActivityAsync(
                    new Game($"Pickup bot | {prefix}help",
                        ActivityType.Playing,
                        ActivityProperties.Play));

                await host.RunReliablyAsync();
            }

            return 0;
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
            using var scope = _serviceProvider.CreateScope();
            var commandHandler = scope.ServiceProvider.GetRequiredService<CommandHandlerService>();
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
