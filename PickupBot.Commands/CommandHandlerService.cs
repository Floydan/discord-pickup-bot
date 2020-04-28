using System;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Google.Apis.Auth.OAuth2;
using Google.Cloud.Translation.V2;
using Microsoft.Extensions.DependencyInjection;

namespace PickupBot.Commands
{
    public class CommandHandlerService
    {
        private readonly CommandService _commands;
        private readonly DiscordSocketClient _discord;
        private readonly IServiceProvider _services;
        private readonly string _commandPrefix;
        private readonly string _googleTranslateApiKey;

        public CommandHandlerService(IServiceProvider services)
        {
            _commands = services.GetRequiredService<CommandService>();
            _discord = services.GetRequiredService<DiscordSocketClient>();
            _services = services;
            _commandPrefix = Environment.GetEnvironmentVariable("CommandPrefix") ?? "!";
            _googleTranslateApiKey = Environment.GetEnvironmentVariable("GoogleTranslateAPIKey") ?? "";

            // Hook CommandExecuted to handle post-command-execution logic.
            _commands.CommandExecuted += CommandExecutedAsync;
            // Hook MessageReceived so we can process each message to see
            // if it qualifies as a command.
            _discord.MessageReceived += MessageReceivedAsync;
            _discord.ReactionAdded += ReactionAddedAsync;
        }

        private async Task ReactionAddedAsync(Cacheable<IUserMessage, ulong> message, ISocketMessageChannel channel, SocketReaction reaction)
        {
            var msg = await message.GetOrDownloadAsync();

            var messageText = msg?.Resolve();
            
            if(string.IsNullOrWhiteSpace(_googleTranslateApiKey) || string.IsNullOrWhiteSpace(messageText)) return;
            var targetLanguage = GetTargetLanguage(reaction.Emote.Name);

            if(string.IsNullOrEmpty(targetLanguage)) return;

            var translation = await GetTranslation(messageText, targetLanguage) ?? await GetTranslation(messageText, targetLanguage);

            if(translation == null) return;

            var userName = (msg.Author as IGuildUser)?.Nickname ??
                           (msg.Author as IGuildUser)?.Username ?? 
                           msg.Author.Username;

            await channel.SendMessageAsync(embed: new EmbedBuilder
            {
                Author = new EmbedAuthorBuilder { IconUrl = msg.Author.GetAvatarUrl(), Name = userName },
                Description = $"{translation.TranslatedText}{Environment.NewLine + Environment.NewLine}",
                Color = Color.DarkBlue,
                Footer = new EmbedFooterBuilder { Text = "Translation provided by Google Translate and pickup-bot" }
            }.Build());
        }

        private async Task<TranslationResult> GetTranslation(string messageText, string targetLanguage)
        {
            using var client = TranslationClient.CreateFromApiKey(_googleTranslateApiKey, TranslationModel.Base);
            client.Service.HttpClient.DefaultRequestHeaders.Add("referer", "127.0.0.1");

            try
            {
                return await client.TranslateTextAsync(messageText, targetLanguage);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);

                return null;
            }
        }

        private static string GetTargetLanguage(string emote)
        {
            switch (emote)
            {
                case "🇸🇪":
                    return "se";
                case "🇫🇷":
                    return "fr";
                case "🇩🇪":
                    return "de";
                case "🇳🇱":
                    return "nl";
                case "🇳🇴":
                    return "no";
                case "🇫🇮":
                    return "fi";
                case "🇩🇰":
                    return "da";
                case "🇵🇱":
                    return "pl";
                case "🇪🇸":
                    return "es";
                case "🇮🇹":
                    return "it";
                case "🇬🇷":
                    return "gr";
                case "🇬🇧":
                case "🇺🇸":
                    return "en";
                default:
                    return null;
            }
        }

        public async Task InitializeAsync()
        {
            // Register modules that are public and inherit ModuleBase<T>.
            
            await _commands.AddModulesAsync(Assembly.GetEntryAssembly(), _services);
            await _commands.AddModulesAsync(GetType().Assembly, _services);
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

        private static async Task CommandExecutedAsync(Optional<CommandInfo> command, ICommandContext context, IResult result)
        {
            // command is unspecified when there was a search failure (command not found); we don't care about these errors
            if (!command.IsSpecified)
                return;

            // the command was successful, we don't care about this result, unless we want to log that a command succeeded.

            if (result.IsSuccess && command.Value.Name == "promote")
            {
                //TODO
                //save when the command was used so we can check against this to prevent spamming
                //e.g. only allow !promote once per hour
            }

            if (result.IsSuccess)
                return;

            // the command failed, let's notify the user that something happened.
            await context.Channel.SendMessageAsync($"error: {result}");
        }
    }
}
