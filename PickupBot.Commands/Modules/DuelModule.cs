using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Microsoft.Extensions.Logging;
using PickupBot.Commands.Extensions;
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
            var duellistRole = await GetDuellistRole().ConfigureAwait(false);

            var user = (IGuildUser)Context.User;
            _logger.LogInformation($"{nameof(Register)} called for user '{user.Username}' with skill level '{level}'");

            var skillLevel = Parse(level);

            var result = await _duelPlayerRepository.Save(user, Parse(level)).ConfigureAwait(false);

            if (user.RoleIds.All(r => r != duellistRole.Id))
                await user.AddRoleAsync(duellistRole).ConfigureAwait(false);

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
                var duellistRole = await GetDuellistRole().ConfigureAwait(false);
                duelPlayer.Active = false;

                if (user.RoleIds.Any(r => r == duellistRole.Id))
                    await user.RemoveRoleAsync(duellistRole).ConfigureAwait(false);

                var result = await _duelPlayerRepository.Save(duelPlayer).ConfigureAwait(false);

                _logger.LogInformation($"Duel player updated to inactive result: {result}");

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
        public async Task Challenge(IGuildUser challengee = null)
        {
            var duellistRole = await GetDuellistRole();

            if (challengee != null)
            {
                var activeClients = challengee.ActiveClients.Select((type, i) => $"{i}. {type.ToString()}").ToList();
                if (challengee.RoleIds.All(r => r != duellistRole.Id))
                {
                    await ReplyAsync($"'{PickupHelpers.GetNickname(challengee)}' is not a duellist.").AutoRemoveMessage(10);
                    return;
                }

                if ((challengee.Status == UserStatus.Online || challengee.Status == UserStatus.Idle) &&
                    challengee.ActiveClients.Any(w => w == ClientType.Desktop || w == ClientType.Web))
                {
                    //challengee is probably on a computer and is online/idle
                    //TODO: If challengee is an active DuelPlayer and no active challenge exists between both opponents
                    // - Create challenge and notify challengee and channel 

                    await _duelMatchRepository.Save((IGuildUser)Context.User, challengee);
                }
                else
                {
                    await ReplyAsync($"'{PickupHelpers.GetNickname(challengee)}' is not online, maybe try again later?").AutoRemoveMessage(10);
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
        [Summary("Accepts a challenge from a specific user e.g. !accept @challenger")]
        public async Task Accept(IGuildUser challenger)
        {
            var match = await _duelMatchRepository.Find(challenger, (IGuildUser)Context.User);
            if (match != null)
            {
                match.Started = true;
                match.MatchDate = DateTime.UtcNow;
                await _duelMatchRepository.Save(match).ConfigureAwait(false);
                //On accept start match
                await ReplyAsync($"{Context.User.Mention} has accepted the challenge from {challenger.Mention}!");
            }
        }

        [Command("decline"), Alias("refuse", "deny")]
        [Summary("Declines a challenge from a specific user e.g. !accept @challenger")]
        public async Task Decline(IGuildUser challenger)
        {
            var match = await _duelMatchRepository.Find(challenger, (IGuildUser)Context.User);
            if (match != null)
            {
                await _duelMatchRepository.Delete(match).ConfigureAwait(false);
                await ReplyAsync($"{Context.User.Mention} has declined the challenge from {challenger.Mention}!");
            }
        }

        [Command("challenges")]
        [Summary("Lists everyone who has challenged you")]
        public async Task Challenges()
        {
            var challenges = await _duelMatchRepository.FindByChallengeeId((IGuildUser)Context.User);
            var duelMatches = challenges as DuelMatch[] ?? challenges.ToArray();
            if (duelMatches.Any(c => !c.Started))
            {
                var challengerIds = duelMatches.Where(c => !c.Started).Select(c => Convert.ToUInt64(c.ChallengerId));
                var users = Context.Guild.Users.Where(u => challengerIds.Contains(u.Id)).ToArray();
                var sb = new StringBuilder();
                foreach (var duelMatch in duelMatches.Where(m => !m.Started).OrderBy(m => m.ChallengeDate))
                {
                    var challenger = users.FirstOrDefault(u => u.Id == Convert.ToUInt64(duelMatch.ChallengerId));
                    if (challenger == null) continue;

                    sb.AppendLine($"{duelMatch.ChallengeDate:yyyy-MM-dd HH:mm:ss 'UTC'} {PickupHelpers.GetNickname(challenger)}");
                }

                await ReplyAsync($"**These brave souls have challenged you to a duel**\n{sb}");
            }
        }

        [Command("opponents"), Alias("victims", "targets")]
        [Summary("Lists everyone you can challenge")]
        public async Task Opponents()
        {
            var challenges = await _duelMatchRepository.FindByChallengerId((IGuildUser)Context.User);
            var duelMatches = challenges as DuelMatch[] ?? challenges.ToArray();
            if (duelMatches.Any(c => !c.Started))
            {
                var challengerIds = duelMatches.Where(c => !c.Started).Select(c => Convert.ToUInt64(c.ChallengerId));
                var users = Context.Guild.Users.Where(u => challengerIds.Contains(u.Id)).ToArray();
                var sb = new StringBuilder();
                foreach (var duelMatch in duelMatches.Where(m => !m.Started).OrderBy(m => m.ChallengeDate))
                {
                    var challengee = users.FirstOrDefault(u => u.Id == Convert.ToUInt64(duelMatch.ChallengeeId));
                    if (challengee == null) continue;

                    sb.AppendLine($"{duelMatch.ChallengeDate:yyyy-MM-dd HH:mm:ss 'UTC'} {PickupHelpers.GetNickname(challengee)}");
                }

                await ReplyAsync($"**These are the foolish mortals you have challenged**\n{sb}");
            }
        }

        [Command("win")]
        [Summary("Record a win")]
        public async Task Win(IGuildUser opponent)
        {
            var match = await _duelMatchRepository.Find(opponent, (IGuildUser)Context.User) ??
                        await _duelMatchRepository.Find((IGuildUser)Context.User, opponent);

            if (match != null && match.Started)
            {
                match.WinnerId = Context.User.Id.ToString();
                match.WinnerName = PickupHelpers.GetNickname(Context.User);
                match.LooserId = opponent.Id.ToString();
                match.LooserName = PickupHelpers.GetNickname(opponent);

                await _duelMatchRepository.Save(match);

                //TODO: Get challenge, if started record a win for winner and loss for looser and delete challenge
                await ReplyAsync($"{match.WinnerName} has won against {match.LooserName}").AutoRemoveMessage(10);

                var winner = await _duelPlayerRepository.Find((IGuildUser)Context.User);
                winner.WonMatches.Add(match);
                await _duelPlayerRepository.Save(winner);

                var looser = await _duelPlayerRepository.Find(opponent);
                winner.LostMatches.Add(match);
                await _duelPlayerRepository.Save(looser);
            }
        }

        [Command("loss")]
        [Summary("Record a loss")]
        public async Task Loss(IGuildUser opponent)
        {
            var match = await _duelMatchRepository.Find(opponent, (IGuildUser)Context.User) ??
                        await _duelMatchRepository.Find((IGuildUser)Context.User, opponent);

            if (match != null && match.Started)
            {
                match.Started = false;
                match.WinnerId = opponent.Id.ToString();
                match.WinnerName = PickupHelpers.GetNickname(opponent);
                match.LooserId = Context.User.Id.ToString();
                match.LooserName = PickupHelpers.GetNickname(Context.User);

                await _duelMatchRepository.Save(match);

                //TODO: Get challenge, if started record a win for winner and loss for looser and delete challenge
                await ReplyAsync($"{match.LooserName} has lost against {match.WinnerName}").AutoRemoveMessage(10);

                var winner = await _duelPlayerRepository.Find(opponent);
                winner.WonMatches.Add(match);
                await _duelPlayerRepository.Save(winner);

                var looser = await _duelPlayerRepository.Find((IGuildUser)Context.User);
                winner.LostMatches.Add(match);
                await _duelPlayerRepository.Save(looser);
            }
        }

        [Command("stats")]
        [Summary("User duel stats")]
        public async Task Stats(IGuildUser user = null)
        {
            user ??= (IGuildUser)Context.User;

            var duelPlayer = await _duelPlayerRepository.Find(user);
            if (duelPlayer == null)
            {
                await ReplyAsync($"{PickupHelpers.GetNickname(user)} is not a duellist.").AutoRemoveMessage(10);
            }
            else
            {
                var games = duelPlayer.WonMatches;
                games.AddRange(duelPlayer.LostMatches);

                var last10 = games.OrderByDescending(g => g.MatchDate)
                                                   .Take(10)
                                                   .Select(g =>
                                                       $"{(Convert.ToUInt64(g.WinnerId) == user.Id ? $":first_place: Won against {g.LooserName}" : $"Lost against {g.WinnerName}")}");
                //TODO: Print table with wins, losses and 10 most recent games
                var embed = new EmbedBuilder
                {
                    Title = $"{PickupHelpers.GetNickname(user)} stats"
                };
                embed.WithFields(new List<EmbedFieldBuilder>
                {
                    new EmbedFieldBuilder { Name = ":first_place: Wins", Value = duelPlayer.Wins, IsInline = true},
                    new EmbedFieldBuilder { Name = "Losses", Value = duelPlayer.Losses, IsInline = true },
                    new EmbedFieldBuilder { Name = "\u200b", Value = "\u200b", IsInline = true},
                    new EmbedFieldBuilder
                    {
                        Name = "Last matches",
                        Value = string.Join(Environment.NewLine, last10)
                    }
                });

                await ReplyAsync(embed: embed.Build());
            }
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

        private async Task<IRole> GetDuellistRole() => Context.Guild.Roles.FirstOrDefault(r => r.Name.Equals("duellist", StringComparison.OrdinalIgnoreCase))
        ?? (IRole)await Context.Guild.CreateRoleAsync("duellist", GuildPermissions.None, isHoisted: false, isMentionable: false);

    }
}
