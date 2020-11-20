using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;
using AutoMapper;
using AutoMapper.EquivalencyExpression;
using Discord;
using Discord.Addons.Hosting;
using Discord.Addons.Interactive;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using PickupBot.Commands;
using PickupBot.Commands.Extensions;
using PickupBot.Commands.Infrastructure;
using PickupBot.Commands.Infrastructure.Services;
using PickupBot.Data.Models;
using PickupBot.GitHub;
using PickupBot.Infrastructure;
using PickupBot.Translation.Models;
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
                .ConfigureDiscordHost<DiscordSocketClient>((context, config) =>
                {
                    config.SocketConfig = new DiscordSocketConfig
                    {
                        LogLevel = LogSeverity.Info,
                        AlwaysDownloadUsers = true,
                        MessageCacheSize = 100
                    };
                    config.Token = context.Configuration["PickupBot:DiscordToken"];
                })
                .UseCommandService((context, conf) =>
                {
                    conf.LogLevel = LogSeverity.Info;
                    conf.DefaultRunMode = RunMode.Async;
                })
                .ConfigureServices(ConfigureServices)
                .UseConsoleLifetime();

            using (var host = builder.Build())
            {
                _serviceProvider = host.Services;
                using var scope = _serviceProvider.CreateScope();
                var client = scope.ServiceProvider.GetRequiredService<DiscordSocketClient>();
                var pickupBotSettings = scope.ServiceProvider.GetRequiredService<PickupBotSettings>();

                client.JoinedGuild += OnJoinedGuild;
                client.MessageUpdated += OnMessageUpdated;

                await host.StartAsync();

                var prefix = pickupBotSettings.CommandPrefix ?? "!";
                await client.SetActivityAsync(
                    new Game($"Pickup bot | {prefix}help",
                        ActivityType.Playing,
                        ActivityProperties.Play));

                await host.WaitForShutdownAsync();
            }

            return 0;
        }

        private static void ConfigureServices(HostBuilderContext hostContext, IServiceCollection services)
        {

            services.Configure<HostOptions>(option =>
            {
                option.ShutdownTimeout = TimeSpan.FromSeconds(5);
            });

            var assemblies = new[]
            {
                Assembly.GetExecutingAssembly(),
                Assembly.GetAssembly(typeof(CommandHandlerService)),
                Assembly.GetAssembly(typeof(PickupQueue)),
                Assembly.GetAssembly(typeof(GitHubService)),
                Assembly.GetAssembly(typeof(TranslationResult))
            };

            services
                .AddHttpClient()
                .ConfigureSettings<PickupBotSettings>(hostContext.Configuration.GetSection("PickupBot"))
                .ConfigureSettings<EncryptionSettings>(hostContext.Configuration.GetSection("Encryption"))
                .AddSingleton<InteractiveService>()
                .AddSingleton<AppVersionInfo>()
                .AddTransient<IListCommandService, ListCommandService>()
                .AddTransient<IMiscCommandService, MiscCommandService>()
                .AddTransient<ISubscriberCommandService, SubscriberCommandService>()
                .AddSingleton<ITranslationService, GoogleTranslationService>()
                .AddSingleton<CommandHandlerService>() //added as singleton to be used in event registration below
                .AddHostedService(p => p.GetService<CommandHandlerService>())
                .AddSingleton<HttpClient>()
                .AddRepositories(hostContext.Configuration)
                .AddAutoMapper(config =>
                {
                    config.AddCollectionMappers();
                    config.AddMaps(assemblies);
                }, assemblies);

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
                else
                {
                    b.AddFilter<ConsoleLoggerProvider>((category, level) => category == "A" || level == LogLevel.Warning);
                }
            });
        }

        private static async Task OnJoinedGuild(SocketGuild guild)
        {
            try
            {
                var pickupsCategory =
                    (ICategoryChannel)guild.CategoryChannels.FirstOrDefault(c => c.Name.Equals("Pickups", StringComparison.OrdinalIgnoreCase)) ??
                    await guild.CreateCategoryChannelAsync("Pickups");

                await CreateChannel(guild,
                    "pickup",
                    "powered by pickup-bot | !help for instructions",
                    pickupsCategory.Id);

                await CreateChannel(guild,
                    "duel",
                    "powered by pickup-bot | !help for instructions",
                    pickupsCategory.Id);

                await CreateChannel(guild,
                    "active-pickup",
                    "Active pickups | use reactions to signup | powered by pickup-bot",
                    pickupsCategory.Id);

                // create applicable roles if missing
                if (guild.Roles.All(w => w.Name != "pickup-promote"))
                    await guild.CreateRoleAsync("pickup-promote", GuildPermissions.None, Color.Orange, isHoisted: false, isMentionable: true);

                // create applicable roles if missing
                if (guild.Roles.All(w => w.Name != "duellist"))
                    await guild.CreateRoleAsync("duellist", GuildPermissions.None, isHoisted: false, isMentionable: true);

                // create voice channel category if missing
                if (guild.CategoryChannels.FirstOrDefault(c => c.Name.Equals("Pickup voice channels", StringComparison.OrdinalIgnoreCase)) == null)
                    await guild.CreateCategoryChannelAsync("Pickup voice channels");
            }
            catch (Exception e)
            {
                await LogAsync(new LogMessage(LogSeverity.Error, nameof(OnJoinedGuild), e.Message, e));
            }
        }

        private static async Task CreateChannel(SocketGuild guild, string name, string topic, ulong categoryId)
        {

            // create #pickup channel if missing
            var activePickupsChannel = guild.Channels.FirstOrDefault(c => c.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (activePickupsChannel == null)
            {
                await guild.CreateTextChannelAsync(name, properties =>
                {
                    properties.Topic = topic;
                    properties.CategoryId = categoryId;
                });
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
