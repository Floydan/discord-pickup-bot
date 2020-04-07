using System.Threading.Tasks;
using Discord;
using Discord.Addons.CommandsExtension;
using Discord.Commands;

namespace PickupBot.Commands.Modules
{
    public class PublicModule : ModuleBase<SocketCommandContext>
    {
        private readonly CommandService _commandService;

        public PublicModule(CommandService commandService)
        {
            _commandService = commandService;
        }

        [Command("ping")]
        [Alias("pong", "hello")]
        public Task PingAsync()
            => ReplyAsync("pong!");

        [Command("userinfo")]
        [Summary("Returns info about the current user, or the user parameter, if one passed.")]
        [Alias("user", "whois")]
        public async Task UserInfoAsync(
            [Summary("The (optional) user to get info from")]
            IUser user = null)
        {
            user ??= Context.User;

            //await ReplyAsync(user.ToString());
            await Context.Channel.SendMessageAsync(user.ToString());
        }
        

        // [Remainder] takes the rest of the command's arguments as one argument, rather than splitting every space
        [Command("echo")]
        public Task EchoAsync([Remainder] string text)
            // Insert a ZWSP before the text to prevent triggering other bots!
            => ReplyAsync('\u200B' + text);

        [Command("help"), Alias("assist"), Summary("Shows help menu."), Remarks("test remark")]
        public async Task Help([Remainder] string command = null)
        {
            const string botPrefix = "!";
            var helpEmbed = _commandService.GetDefaultHelpEmbed(command, botPrefix);
            await Context.Channel.SendMessageAsync(embed: helpEmbed);
        }
    }
}
