using System;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Microsoft.Extensions.Logging;
using PickupBot.Commands.Infrastructure.Helpers;
using PickupBot.Data.Models;
using PickupBot.Data.Repositories;

namespace PickupBot.Commands.Modules
{
    [Name("duel")]
    [Summary("Manages duel registration and challenges")]
    public class DuelModule : ModuleBase<SocketCommandContext>
    {
        private readonly IDuelPlayerRepository _duelPlayerRepository;
        private readonly IDuelMatchRepository _duelMatchRepository;
        private readonly ILogger<DuelModule> _logger;

        public DuelModule(IDuelPlayerRepository duelPlayerRepository, IDuelMatchRepository duelMatchRepository, ILogger<DuelModule> logger)
        {
            _duelPlayerRepository = duelPlayerRepository;
            _duelMatchRepository = duelMatchRepository;
            _logger = logger;
        }

        [Command("register")]
        [Summary("Register for duels")]
        public async Task Register([Name("skill level (low, medium, high)"), Remainder]string level = "")
        {
            var user = (IGuildUser)Context.User;
            _logger.LogInformation($"{nameof(Register)} called for user '{user.Username}' with skill level '{level}'");

            var skillLevel = Parse(level);

            var result = await _duelPlayerRepository.Save(user, Parse(level)).ConfigureAwait(false);

            _logger.LogInformation($"Duel player saved result: {result}");

            await ReplyAsync($"You have been registered for duels with skill level [{skillLevel.ToString()}]");
        }

        [Command("unregister")]
        [Summary("Unregister for duels")]
        public async Task UnRegister()
        {
            var user = (IGuildUser)Context.User;
            _logger.LogInformation($"{nameof(UnRegister)} called for user '{user.Username}'");

            var duelPlayer = await _duelPlayerRepository.Find(user);
            if (duelPlayer != null)
            {
                duelPlayer.Active = false;

                var result = await _duelPlayerRepository.Save(duelPlayer).ConfigureAwait(false);

                _logger.LogInformation($"Duel player update to inactive result: {result}");

                await ReplyAsync("You have been unregistered for duels.");
            }
        }

        [Command("setskill")]
        [Summary("Set skill level")]
        public async Task SetSkill([Name("skill level (low, medium, high)"), Remainder]string level = "")
        {
            var user = (IGuildUser)Context.User;
            _logger.LogInformation($"{nameof(SetSkill)} called for user '{user.Username}' with skill level '{level}'");

            var skillLevel = Parse(level);

            var result = await _duelPlayerRepository.Save(user, skillLevel).ConfigureAwait(false);

            _logger.LogInformation($"Duel player saved result: {result}");

            await ReplyAsync($"Your skill level has been set to [{skillLevel.ToString()}]");
        }

        [Command("challenge")]
        public async Task Challenge(IUser challengee = null)
        {
            if (challengee != null)
            {
                var activeClients = challengee.ActiveClients.Select((type, i) => $"{i}. {type.ToString()}").ToList();
                if ((challengee.Status == UserStatus.Online || challengee.Status == UserStatus.Idle) &&
                    challengee.ActiveClients.Any(w => w == ClientType.Desktop || w == ClientType.Web))
                {
                    //challangee is probably on a computer and is online/idle
                    //TODO: If challengee is an active DuelPlayer
                    //Create challenge and notify challangee and channel 
                }

                await ReplyAsync($"{challengee.Username}: {challengee.Status.ToString()}; Clients: [{string.Join(", ", activeClients)}]");
            }
            else
            {
                //TODO: Get list of all online opponents who are active
                // 1. sort on skill level
                // 2. Select first with same skill level or first in skill level above
            }
        }

        [Command("accept")]
        [Summary("Accepts a challange from a specific user e.g. !accept @username")]
        public async Task Accept(IUser challenger)
        {
            //TODO: On accept start match
            await ReplyAsync($"{Context.User.Mention} has accepted the challange from {challenger.Mention}!");
        }

        [Command("decline"), Alias("refuse", "deny")]
        [Summary("Declines a challange from a specific user e.g. !accept @challenger")]
        public async Task Decline(IUser challanger)
        {
            //TODO: delete challange
            await ReplyAsync($"{Context.User.Mention} has declined the challange from {challanger.Mention}!");
        }

        [Command("challanges"), Alias("refuse", "deny")]
        [Summary("Lists everyone who has challanged you")]
        public async Task Challenges()
        {
            //TODO: List all open challenges where Context.User is "target"
            await ReplyAsync("Here are all your challangers");
        }

        [Command("opponents"), Alias("victims", "targets")]
        [Summary("Lists everyone you can challange")]
        public async Task Opponents()
        {
            //TODO: Get all online users with the duel role
            await ReplyAsync("List of all possible online opponents");
        }

        [Command("win")]
        [Summary("Record a win")]
        public async Task Win(IUser opponent)
        {
            //TODO: Get challenge, if started record a win for winner and loss for looser and delete challenge
            await ReplyAsync("Records a win agains an opponent");
        }

        [Command("loss")]
        [Summary("Record a loss")]
        public async Task Loss(IUser opponent)
        {
            //TODO: Get challenge, if started record a win for winner and loss for looser and delete challenge
            await ReplyAsync("Records a loss agains an opponent");
        }

        private static SkillLevel Parse(string level)
        {
            var skillLevel = SkillLevel.Medium;
            if (!string.IsNullOrWhiteSpace(level))
            {
                skillLevel = Enum.Parse<SkillLevel>(level, true);
            }

            return skillLevel;
        }
    }
}
