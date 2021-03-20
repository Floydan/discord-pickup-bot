using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Discord;
using Discord.Addons.Hosting;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using PickupBot.Commands.Infrastructure;
using PickupBot.Commands.Infrastructure.Utilities;
using PickupBot.Commands.Models;
using PickupBot.Data.Models;
using PickupBot.Translation.Services;

namespace PickupBot.Commands
{
    public class CommandHandlerService : InitializedService, IDisposable
    {
        private readonly CommandService _commands;
        private readonly DiscordSocketClient _discord;
        private readonly IServiceProvider _services;
        private readonly ILogger<CommandHandlerService> _logger;
        private readonly string _commandPrefix;
        private readonly ITranslationService _translationService;
        private IActivity _currentActivity;
        private readonly string _rconPassword;
        private readonly string _rconHost;
        private readonly int _rconPort;

        public CommandHandlerService(IServiceProvider services, PickupBotSettings pickupBotSettings, ILogger<CommandHandlerService> logger)
        {
            ServiceLocator.SetLocatorProvider(services);
            _commands = services.GetRequiredService<CommandService>();
            _discord = services.GetRequiredService<DiscordSocketClient>();
            _translationService = services.GetService<ITranslationService>();
            _services = services;
            _logger = logger;
            _commandPrefix = pickupBotSettings.CommandPrefix ?? "!";

            _rconPassword = pickupBotSettings.RCONServerPassword ?? "";
            _rconHost = pickupBotSettings.RCONHost ?? "";
            int.TryParse(pickupBotSettings.RCONPort ?? "0", out _rconPort);

            // Hook CommandExecuted to handle post-command-execution logic.
            _commands.CommandExecuted += CommandExecutedAsync;
            // Hook MessageReceived so we can process each message to see
            // if it qualifies as a command.
            _discord.MessageReceived += MessageReceivedAsync;
            _discord.ReactionAdded += ReactionAddedAsync;

            //if (_commandPrefix == "!")
            //{
            //    GetActivityStats().GetAwaiter().GetResult();
            //    UpdateActvity();
            //}

            //TODO: test for dynamic db driven commands
            //_commands.CreateModuleAsync("dyntest", builder =>
            //{
            //    builder.AddCommand("dynhello", (context, objects, serv, callback) =>
            //    {
            //        _logger.LogInformation(message: JsonConvert.SerializeObject(objects));
            //        return Task.FromResult(true);
            //    }, create =>
            //    {
            //        create.AddParameter("name", typeof(string), pb =>
            //        {
            //            pb.IsOptional = true;
            //            pb.DefaultValue = "world!";
            //        });
            //    });
            //}).GetAwaiter().GetResult();
        }

        private void UpdateActvity()
        {
            if (string.IsNullOrWhiteSpace(_rconPassword) ||
               string.IsNullOrWhiteSpace(_rconHost) ||
               _rconPort <= 0) return;

            _currentActivity = _discord.Activity;

            AsyncUtilities.DelayAction(TimeSpan.FromSeconds(15), async _ =>
            {
                await _discord.SetActivityAsync(_currentActivity);
                UpdateActvity();
            });

        }

        private async Task GetActivityStats()
        {
            if (string.IsNullOrWhiteSpace(_rconPassword) ||
                string.IsNullOrWhiteSpace(_rconHost) ||
                _rconPort <= 0) return;

            try
            {
                var status = await RCON.UDPSendCommand("status", _rconHost, _rconPassword, _rconPort);
                var serverStatus = new ServerStatus(status);
                var activity = new Game($"{serverStatus.Players.Count}",
                    ActivityType.Playing,
                    ActivityProperties.Play);

                await _discord.SetActivityAsync(activity);

                _currentActivity = activity;

                AsyncUtilities.DelayAction(TimeSpan.FromMinutes(1), async _ => { await GetActivityStats(); });
            }
            catch (Exception e)
            {
                _logger.LogError(e, e.Message);
            }
        }

        private async Task ReactionAddedAsync(Cacheable<IUserMessage, ulong> message, ISocketMessageChannel channel, SocketReaction reaction)
        {
            if (_translationService == null) return;

            var msg = await message.GetOrDownloadAsync();

            var messageText = msg?.Resolve();
            var targetLanguage = _translationService.GetTargetLanguage(reaction.Emote.Name);

            if (string.IsNullOrWhiteSpace(messageText) || string.IsNullOrEmpty(targetLanguage)) return;

            var translations = (await _translationService.Translate(targetLanguage, messageText, "Original message") ??
                              await _translationService.Translate(targetLanguage, messageText, "Original message")).ToList();

            if (!translations.Any()) return;

            var userName = (msg.Author as IGuildUser)?.Nickname ??
                           (msg.Author as IGuildUser)?.Username ??
                           msg.Author.Username;

            var sentMessage = await channel.SendMessageAsync(embed: new EmbedBuilder
            {
                Author = new EmbedAuthorBuilder { IconUrl = msg.Author.GetAvatarUrl(), Name = userName },
                Description = $"{translations.FirstOrDefault()?.TranslatedText}{Environment.NewLine + Environment.NewLine}" +
                              $"[{translations.LastOrDefault()?.TranslatedText} :arrow_up:]({msg.GetJumpUrl()})",
                Color = Color.DarkBlue,
                Footer = new EmbedFooterBuilder
                {
                    Text = $"Translation provided by Google Translate and pickup-bot.{Environment.NewLine}" +
                                                         $"This message will self destruct in 30 seconds."
                }
            }.Build());

            AsyncUtilities.DelayAction(TimeSpan.FromSeconds(30), async t => { await sentMessage.DeleteAsync(); });
        }

        public async Task MessageReceivedAsync(SocketMessage rawMessage)
        {
            // Ignore system messages, or messages from other bots
            if (!(rawMessage is SocketUserMessage message)) return;
            if (message.Source != MessageSource.User) return;

            // This value holds the offset where the prefix ends
            var argPos = 0;

            if (!message.HasStringPrefix(_commandPrefix, ref argPos)) return;

            var context = new SocketCommandContext(_discord, message);

            // Perform the execution of the command. In this method,
            // the command service will perform precondition and parsing check
            // then execute the command if one is matched.
            await _commands.ExecuteAsync(context, argPos, _services);
        }

        private async Task CommandExecutedAsync(Optional<CommandInfo> command, ICommandContext context, IResult result)
        {
            if (!string.IsNullOrEmpty(result?.ErrorReason))
            {
                await context.Channel.SendMessageAsync(result.ErrorReason);
            }

            if (!result?.IsSuccess ?? false)
            {
                var commandName = command.IsSpecified ? command.Value.Name : "A command";
                LogAsync(new LogMessage(LogSeverity.Info,
                    "CommandExecution",
                    $"{commandName} was executed at {DateTime.UtcNow}."));
            }

            // command is unspecified when there was a search failure (command not found); we don't care about these errors
            if (!command.IsSpecified)
                return;

            // the command was successful, we don't care about this result

            if ((result?.IsSuccess ?? false) && command.Value.Name == "promote")
            {
                //TODO
                //save when the command was used so we can check against this to prevent spamming
                //e.g. only allow !promote once per hour
            }
        }

        public override async Task InitializeAsync(CancellationToken cancellationToken)
        {
            await _commands.AddModulesAsync(Assembly.GetEntryAssembly(), _services);
            await _commands.AddModulesAsync(GetType().Assembly, _services);
        }

        private void LogAsync(LogMessage logMessage)
        {
            _logger.Log(SeverityToLogLevel(logMessage.Severity), logMessage.Exception, logMessage.Message, logMessage.Source);
            if (logMessage.Exception is CommandException cmdException)
            {
                // We can also log this incident
                Console.WriteLine($"{cmdException.Context.User} failed to execute '{cmdException.Command.Name}' in {cmdException.Context.Channel}.");
                Console.WriteLine(cmdException.ToString());
            }
        }

        private static LogLevel SeverityToLogLevel(LogSeverity severity)
        {
            return severity switch
            {
                LogSeverity.Critical => LogLevel.Critical,
                LogSeverity.Error => LogLevel.Error,
                LogSeverity.Warning => LogLevel.Warning,
                LogSeverity.Info => LogLevel.Information,
                LogSeverity.Verbose => LogLevel.Trace,
                LogSeverity.Debug => LogLevel.Debug,
                _ => LogLevel.Information
            };
        }

        public void Dispose()
        {
            //remove event handlers to keep things clean on dispose
            _commands.CommandExecuted -= CommandExecutedAsync;
            _discord.MessageReceived -= MessageReceivedAsync;
            _discord.ReactionAdded -= ReactionAddedAsync;

            ((IDisposable)_commands)?.Dispose();
            _discord?.Dispose();
        }
    }
}
