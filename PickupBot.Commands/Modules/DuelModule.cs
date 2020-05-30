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
            if(!PickupHelpers.IsInDuelChannel(Context.Channel)) return;

            var duellistRole = await GetDuellistRole().ConfigureAwait(false);

            var skillLevel = Parse(level);
            var user = (IGuildUser)Context.User;
            _logger.LogInformation($"{nameof(Register)} called for user '{user.Username}' with skill level '{skillLevel.ToString()}'");
            
            var result = await _duelPlayerRepository.Save(user, Parse(level)).ConfigureAwait(false);

            if (user.RoleIds.All(r => r != duellistRole.Id))
                await user.AddRoleAsync(duellistRole).ConfigureAwait(false);

            _logger.LogInformation($"Duel player saved result: {result}");

            await ReplyAsync($"You have been registered for duels with skill level [{skillLevel.ToString()}]").AutoRemoveMessage(10);
        }

        [Command("unregister")]
        [Summary("Unregister for duels")]
        public async Task UnRegister()
        {
            if(!PickupHelpers.IsInDuelChannel(Context.Channel)) return;

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

                await ReplyAsync("You have been unregistered for duels.").AutoRemoveMessage(10);
            }
        }

        [Command("setskill")]
        [Summary("Set skill level")]
        public async Task SetSkill([Name("skill level (low, medium, high)"), Remainder]string level = "")
        {
            if(!PickupHelpers.IsInDuelChannel(Context.Channel)) return;

            var user = (IGuildUser)Context.User;
            _logger.LogInformation($"{nameof(SetSkill)} called for user '{user.Username}' with skill level '{level}'");

            var skillLevel = Parse(level);

            var result = await _duelPlayerRepository.Save(user, skillLevel).ConfigureAwait(false);

            _logger.LogInformation($"Duel player saved result: {result}");

            await ReplyAsync($"Your skill level has been set to [{skillLevel.ToString()}]").AutoRemoveMessage(10);
        }

        [Command("challenge")]
        public async Task Challenge(IGuildUser challengee = null)
        {
            if(!PickupHelpers.IsInDuelChannel(Context.Channel)) return;

            var user = (IGuildUser)Context.User;
            _logger.LogInformation($"{nameof(Challenge)} called for user '{user.Username}'");

            if (challengee != null && (challengee.Id == Context.User.Id || challengee.IsBot)) return;

            var duellistRole = await GetDuellistRole();

            var duelPlayer = await _duelPlayerRepository.Find(user);
            if (duelPlayer == null)
            {
                await ReplyAsync("You are not a duellist, `!register <skill level>` to become one.").AutoRemoveMessage(10);
                return;
            }

            if (challengee != null)
            {
                if (challengee.RoleIds.All(r => r != duellistRole.Id))
                {
                    await ReplyAsync($"'{PickupHelpers.GetNickname(challengee)}' is not a duellist.").AutoRemoveMessage(10);
                    return;
                }

                if (CheckUserOnlineState(challengee))
                {
                    await _duelMatchRepository.Save((IGuildUser)Context.User, challengee).ConfigureAwait(false);
                    await ReplyAsync($"{Context.User.Mention} has challenged {challengee.Mention} to a duel!");
                }
                else
                {
                    await ReplyAsync($"'{PickupHelpers.GetNickname(challengee)}' is not online, maybe try again later?").AutoRemoveMessage(10);
                }
            }
            else
            {
                var duellistUsers = Context.Guild.Users.Where(u => u.Roles.Contains(duellistRole))
                    .Where(CheckUserOnlineState)
                    .ToArray();

                if (duellistUsers.Any())
                {
                    var duellists = new List<DuelPlayer>();
                    foreach (var duellistUser in duellistUsers)
                    {
                        var duellistPlayer = await _duelPlayerRepository.Find(duellistUser);
                        if (duellistPlayer != null) duellists.Add(duellistPlayer);
                    }

                    var opponent = duellists.FirstOrDefault(d => d.Skill >= duelPlayer.Skill) ?? duellists.FirstOrDefault();

                    if (opponent == null)
                    {
                        await ReplyAsync("No opponents could be found.").AutoRemoveMessage(10);
                    }
                    else
                    {
                        var opponentUser = duellistUsers.First(u => u.Id == opponent.Id);
                        await _duelMatchRepository.Save((IGuildUser)Context.User, opponentUser).ConfigureAwait(false);
                        await ReplyAsync($"{Context.User.Mention} has challenged {opponentUser.Mention} to a duel!");
                    }
                }
                else
                {
                    await ReplyAsync("No duellists are currently online").AutoRemoveMessage(10);
                }
            }
        }

        [Command("accept")]
        [Summary("Accepts a challenge from a specific user e.g. !accept @challenger")]
        public async Task Accept(IGuildUser challenger)
        {
            if(!PickupHelpers.IsInDuelChannel(Context.Channel)) return;

            var match = await _duelMatchRepository.Find(challenger, (IGuildUser)Context.User);
            if (match != null)
            {
                //On accept start match

                match.Started = true;
                match.MatchDate = DateTime.UtcNow;
                await _duelMatchRepository.Save(match).ConfigureAwait(false);
                await ReplyAsync($"{Context.User.Mention} has accepted the challenge from {challenger.Mention}!");
            }
        }

        [Command("decline"), Alias("refuse", "deny")]
        [Summary("Declines a challenge from a specific user e.g. !accept @challenger")]
        public async Task Decline(IGuildUser challenger)
        {
            if(!PickupHelpers.IsInDuelChannel(Context.Channel)) return;

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
            if(!PickupHelpers.IsInDuelChannel(Context.Channel)) return;

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

                    sb.AppendLine($" - {PickupHelpers.GetNickname(challenger)} `{duelMatch.ChallengeDate:yyyy-MM-dd HH:mm:ss 'UTC'}`");
                }

                await ReplyAsync($"**These brave souls have challenged you to a duel**\n{sb}").AutoRemoveMessage();
            }
            else
            {
                await ReplyAsync("No one has been brave enough to challenge you").AutoRemoveMessage(10);
            }
        }

        [Command("opponents"), Alias("victims", "targets")]
        [Summary("Lists everyone you have challenged")]
        public async Task Opponents()
        {
            if(!PickupHelpers.IsInDuelChannel(Context.Channel)) return;

            var challenges = await _duelMatchRepository.FindByChallengerId((IGuildUser)Context.User);
            var duelMatches = challenges as DuelMatch[] ?? challenges.ToArray();
            if (duelMatches.Any(c => !c.Started))
            {
                var challengerIds = duelMatches.Where(c => !c.Started).Select(c => Convert.ToUInt64(c.ChallengeeId));
                var users = Context.Guild.Users.Where(u => challengerIds.Contains(u.Id)).ToArray();
                var sb = new StringBuilder();
                foreach (var duelMatch in duelMatches.Where(m => !m.Started).OrderBy(m => m.ChallengeDate))
                {
                    var challengee = users.FirstOrDefault(u => u.Id == Convert.ToUInt64(duelMatch.ChallengeeId));
                    if (challengee == null) continue;

                    sb.AppendLine($" - {PickupHelpers.GetNickname(challengee)} `{duelMatch.ChallengeDate:yyyy-MM-dd HH:mm:ss 'UTC'}`");
                }

                await ReplyAsync($"**These are the foolish mortals you have challenged**\n{sb}").AutoRemoveMessage();
            }
            else
            {
                await ReplyAsync("You have not challenged anyone").AutoRemoveMessage(10);
            }
        }

        [Command("win"), Alias("won")]
        [Summary("Record a win")]
        public async Task Win(IGuildUser opponent)
        {
            if(!PickupHelpers.IsInDuelChannel(Context.Channel)) return;

            var match = await _duelMatchRepository.Find(opponent, (IGuildUser)Context.User) ??
                        await _duelMatchRepository.Find((IGuildUser)Context.User, opponent);

            if (match != null && match.Started)
            {
                await _duelMatchRepository.Delete(match);

                if (!match.MatchDate.HasValue)
                    match.MatchDate = DateTime.UtcNow;

                match.WinnerId = Context.User.Id.ToString();
                match.WinnerName = PickupHelpers.GetNickname(Context.User);
                match.LooserId = opponent.Id.ToString();
                match.LooserName = PickupHelpers.GetNickname(opponent);

                //TODO: Get challenge, if started record a win for winner and loss for looser and delete challenge
                await ReplyAsync($"{match.WinnerName} has won against {match.LooserName}").AutoRemoveMessage(10);

                var winner = await _duelPlayerRepository.Find((IGuildUser)Context.User);
                var looser = await _duelPlayerRepository.Find(opponent);
                UpdateMMR(winner, looser);

                winner.MatchHistory.Insert(0, match);
                await _duelPlayerRepository.Save(winner);

                looser.MatchHistory.Insert(0, match);
                await _duelPlayerRepository.Save(looser);
            }
        }

        [Command("loss"), Alias("lost")]
        [Summary("Record a loss")]
        public async Task Loss(IGuildUser opponent)
        {
            if(!PickupHelpers.IsInDuelChannel(Context.Channel)) return;

            var match = await _duelMatchRepository.Find(opponent, (IGuildUser)Context.User) ??
                        await _duelMatchRepository.Find((IGuildUser)Context.User, opponent);

            if (match != null && match.Started)
            {
                await _duelMatchRepository.Delete(match);

                if (!match.MatchDate.HasValue)
                    match.MatchDate = DateTime.UtcNow;

                match.WinnerId = opponent.Id.ToString();
                match.WinnerName = PickupHelpers.GetNickname(opponent);
                match.LooserId = Context.User.Id.ToString();
                match.LooserName = PickupHelpers.GetNickname(Context.User);

                //TODO: Get challenge, if started record a win for winner and loss for looser and delete challenge
                await ReplyAsync($"{match.LooserName} has lost against {match.WinnerName}").AutoRemoveMessage(10);

                var winner = await _duelPlayerRepository.Find(opponent);
                var looser = await _duelPlayerRepository.Find((IGuildUser)Context.User);

                UpdateMMR(winner, looser);
                winner.MatchHistory.Insert(0, match);
                await _duelPlayerRepository.Save(winner);

                looser.MatchHistory.Insert(0, match);
                await _duelPlayerRepository.Save(looser);
            }
        }

        [Command("stats")]
        [Summary("User duel stats")]
        public async Task Stats(IGuildUser user = null)
        {
            if(!PickupHelpers.IsInDuelChannel(Context.Channel)) return;

            user ??= (IGuildUser)Context.User;

            var duelPlayer = await _duelPlayerRepository.Find(user);
            if (duelPlayer == null)
            {
                await ReplyAsync($"{PickupHelpers.GetNickname(user)} is not a duellist.").AutoRemoveMessage(10);
            }
            else
            {
                var games = duelPlayer.MatchHistory;

                var last10 = games.Take(10)
                                       .Select(g =>
                                           $"{(Convert.ToUInt64(g.WinnerId) == user.Id ? $":first_place: Won against {g.LooserName}" : $":cry: Lost against {g.WinnerName}")}" +
                                           $" (_{g.MatchDate:yyyy-MM-dd HH:mm:ss 'UTC'}_)")
                                       .ToArray();
                //TODO: Print table with wins, losses and 10 most recent games
                var embed = new EmbedBuilder
                {
                    Title = $"{PickupHelpers.GetNickname(user)} stats [{((SkillLevel)duelPlayer.Skill).ToString()}] MMR: {duelPlayer.MMR}",
                    Timestamp = duelPlayer.Timestamp
                };
                embed.WithFields(new List<EmbedFieldBuilder>
                {
                    new EmbedFieldBuilder { Name = ":first_place: Wins", Value = duelPlayer.MatchHistory.Count(w => w.WinnerId == duelPlayer.Id.ToString()), IsInline = true},
                    new EmbedFieldBuilder { Name = "\u200b", Value = "\u200b", IsInline = true},
                    new EmbedFieldBuilder { Name = ":cry: Losses", Value = duelPlayer.MatchHistory.Count(w => w.LooserId == duelPlayer.Id.ToString()), IsInline = true },
                    new EmbedFieldBuilder { Name = "\u200b", Value = "\u200b" },
                    new EmbedFieldBuilder
                    {
                        Name = "Last matches",
                        Value = last10.IsNullOrEmpty() ? "No matches recorded" : string.Join(Environment.NewLine, last10)
                    }
                });

                await ReplyAsync(embed: embed.Build()).AutoRemoveMessage();
            }
        }

        private static bool CheckUserOnlineState(IGuildUser u)
        {
            return (u.Status == UserStatus.Online || u.Status == UserStatus.Idle) && u.ActiveClients.Any(ac => ac == ClientType.Desktop || ac == ClientType.Web);
        }

        // ReSharper disable once InconsistentNaming
        private static void UpdateMMR(DuelPlayer winner, DuelPlayer looser)
        {
            if (looser.Skill > winner.Skill)
            {
                winner.MMR += 50;
                looser.MMR -= 50;
            }
            if (looser.Skill == winner.Skill)
            {
                winner.MMR += 30;
                looser.MMR -= 30;
            };
            if (looser.Skill < winner.Skill)
            {
                winner.MMR += 10;
                looser.MMR -= 10;
            };
        }

        private static SkillLevel Parse(string level)
        {
            var skillLevel = SkillLevel.Medium;
            if (string.IsNullOrWhiteSpace(level)) return skillLevel;

            if (!Enum.TryParse(level, true, out skillLevel))
                skillLevel = SkillLevel.Medium;

            if ((int)skillLevel > 3 || (int)skillLevel < 1)
                skillLevel = SkillLevel.Medium;

            return skillLevel;
        }

        private async Task<IRole> GetDuellistRole() => 
            Context.Guild.Roles.FirstOrDefault(r => r.Name.Equals("duellist", StringComparison.OrdinalIgnoreCase)) ??
            (IRole)await Context.Guild.CreateRoleAsync("duellist", GuildPermissions.None, isHoisted: false, isMentionable: false);

    }
}
