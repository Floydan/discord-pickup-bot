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
        private readonly IDuelChallengeRepository _duelChallengeRepository;
        private readonly IDuelMatchRepository _duelMatchRepository;
        private readonly ILogger<DuelModule> _logger;

        public DuelModule(IDuelPlayerRepository duelPlayerRepository, IDuelChallengeRepository duelChallengeRepository, IDuelMatchRepository duelMatchRepository, ILogger<DuelModule> logger)
        {
            _duelPlayerRepository = duelPlayerRepository;
            _duelChallengeRepository = duelChallengeRepository;
            _duelMatchRepository = duelMatchRepository;
            _logger = logger;
        }

        [Command("register")]
        [Summary("Register for duels")]
        public async Task Register([Name("skill level (low, medium, high)"), Remainder]string level = "")
        {
            if (!PickupHelpers.IsInDuelChannel(Context.Channel)) return;

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
            if (!PickupHelpers.IsInDuelChannel(Context.Channel)) return;

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
            if (!PickupHelpers.IsInDuelChannel(Context.Channel)) return;

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
            if (!PickupHelpers.IsInDuelChannel(Context.Channel)) return;

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
                    await _duelChallengeRepository.Save((IGuildUser)Context.User, challengee).ConfigureAwait(false);
                    await ReplyAsync($"{Context.User.Mention} has challenged {challengee.Mention} to a duel!");
                }
                else
                {
                    await ReplyAsync($"'{PickupHelpers.GetNickname(challengee)}' is not online, maybe try again later?").AutoRemoveMessage(10);
                }
            }
            else
            {
                var duellistUsers = Context.Guild.Users.Where(u => u.Roles.Contains(duellistRole) && u.Id != Context.User.Id)
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

                    // ReSharper disable once InconsistentNaming
                    var closestMMR = duellists.Where(d => d.MMR >= duelPlayer.MMR).OrderBy(d => d.MMR);

                    var opponent = closestMMR.FirstOrDefault(d => d.Skill >= duelPlayer.Skill) ??
                                           duellists.FirstOrDefault(d => d.Skill >= duelPlayer.Skill) ??
                                           duellists.FirstOrDefault();

                    if (opponent == null)
                    {
                        await ReplyAsync("No opponents could be found.").AutoRemoveMessage(10);
                    }
                    else
                    {
                        var opponentUser = duellistUsers.First(u => u.Id == opponent.Id);
                        await _duelChallengeRepository.Save((IGuildUser)Context.User, opponentUser).ConfigureAwait(false);
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
            if (!PickupHelpers.IsInDuelChannel(Context.Channel)) return;
            _logger.LogInformation($"{nameof(Accept)} called for user '{challenger.Username}'");

            var challenge = await _duelChallengeRepository.Find(challenger, (IGuildUser)Context.User);
            if (challenge != null)
            {
                //On accept start match
                await _duelChallengeRepository.Delete(challenge);
                var match = new DuelMatch(Context.Guild.Id, challenger.Id, Context.User.Id)
                {
                    Started = true,
                    ChallengeDate = challenge.ChallengeDate,
                    MatchDate = DateTime.UtcNow
                };
                await _duelMatchRepository.Save(match).ConfigureAwait(false);
                await ReplyAsync($"{Context.User.Mention} has accepted the challenge from {challenger.Mention}!");
            }
        }

        [Command("decline"), Alias("refuse", "deny")]
        [Summary("Declines a challenge from a specific user e.g. !accept @challenger")]
        public async Task Decline(IGuildUser challenger)
        {
            if (!PickupHelpers.IsInDuelChannel(Context.Channel)) return;
            _logger.LogInformation($"{nameof(Decline)} called for user '{challenger.Username}'");

            var challenge = await _duelChallengeRepository.Find(challenger, (IGuildUser)Context.User);
            if (challenge != null)
            {
                await _duelChallengeRepository.Delete(challenge).ConfigureAwait(false);
                await ReplyAsync($"{Context.User.Mention} has declined the challenge from {challenger.Mention}!");
            }
        }

        [Command("challenges")]
        [Summary("Lists everyone who has challenged you")]
        public async Task Challenges()
        {
            if (!PickupHelpers.IsInDuelChannel(Context.Channel)) return;

            var challenges = await _duelChallengeRepository.FindByChallengeeId((IGuildUser)Context.User);
            var duelChallenges = challenges as DuelChallenge[] ?? challenges.ToArray();
            if (duelChallenges.Any())
            {
                var challengerIds = duelChallenges.Select(c => Convert.ToUInt64(c.ChallengerId));
                var users = Context.Guild.Users.Where(u => challengerIds.Contains(u.Id)).ToArray();
                var sb = new StringBuilder();
                foreach (var duelMatch in duelChallenges.OrderBy(m => m.ChallengeDate))
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
            if (!PickupHelpers.IsInDuelChannel(Context.Channel)) return;

            var challenges = await _duelChallengeRepository.FindByChallengerId((IGuildUser)Context.User);
            var duelChallenges = challenges as DuelChallenge[] ?? challenges.ToArray();
            if (duelChallenges.Any())
            {
                var challengerIds = duelChallenges.Select(c => Convert.ToUInt64(c.ChallengeeId));
                var users = Context.Guild.Users.Where(u => challengerIds.Contains(u.Id)).ToArray();
                var sb = new StringBuilder();
                foreach (var duelMatch in duelChallenges.OrderBy(m => m.ChallengeDate))
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
            if (!PickupHelpers.IsInDuelChannel(Context.Channel)) return;
            if (opponent.Id == Context.User.Id) return;

            var matches = (await _duelMatchRepository.FindByChallengeeId((IGuildUser)Context.User)).ToList();
            matches.AddRange(await _duelMatchRepository.FindByChallengerId((IGuildUser)Context.User));

            matches = matches.Where(w => (w.ChallengerId == opponent.Id.ToString() || w.ChallengeeId == opponent.Id.ToString()) && string.IsNullOrEmpty(w.WinnerId)).ToList();

            if (matches.Any())
            {
                var match = matches.First();
                var winner = await _duelPlayerRepository.Find((IGuildUser)Context.User);
                var looser = await _duelPlayerRepository.Find(opponent);

                var mmrDiff = UpdateMMR(winner, looser);

                if (!match.MatchDate.HasValue)
                    match.MatchDate = DateTime.UtcNow;

                match.WinnerId = Context.User.Id.ToString();
                match.WinnerName = PickupHelpers.GetNickname(Context.User);
                match.LooserId = opponent.Id.ToString();
                match.LooserName = PickupHelpers.GetNickname(opponent);
                match.MMR = mmrDiff;

                await _duelMatchRepository.Save(match);

                await ReplyAsync($"{match.WinnerName} has won against {match.LooserName}").AutoRemoveMessage(10);

                winner.MatchHistory.Insert(0, match);
                winner.MatchHistory = winner.MatchHistory.Take(10).ToList();
                await _duelPlayerRepository.Save(winner);

                looser.MatchHistory.Insert(0, match);
                looser.MatchHistory = looser.MatchHistory.Take(10).ToList();
                await _duelPlayerRepository.Save(looser);
            }
        }

        [Command("loss"), Alias("lost")]
        [Summary("Record a loss")]
        public async Task Loss(IGuildUser opponent)
        {
            if (!PickupHelpers.IsInDuelChannel(Context.Channel)) return;
            if (opponent.Id == Context.User.Id) return;

            var matches = (await _duelMatchRepository.FindByChallengeeId((IGuildUser)Context.User)).ToList();
            matches.AddRange(await _duelMatchRepository.FindByChallengerId((IGuildUser)Context.User));

            matches = matches.Where(w => (w.ChallengerId == opponent.Id.ToString() || w.ChallengeeId == opponent.Id.ToString()) && string.IsNullOrEmpty(w.WinnerId)).ToList();

            if (matches.Any())
            {
                var match = matches.First();
                var winner = await _duelPlayerRepository.Find(opponent);
                var looser = await _duelPlayerRepository.Find((IGuildUser)Context.User);

                var mmrDiff = UpdateMMR(winner, looser);

                if (!match.MatchDate.HasValue)
                    match.MatchDate = DateTime.UtcNow;

                match.Started = true;
                match.WinnerId = opponent.Id.ToString();
                match.WinnerName = PickupHelpers.GetNickname(opponent);
                match.LooserId = Context.User.Id.ToString();
                match.LooserName = PickupHelpers.GetNickname(Context.User);
                match.MMR = mmrDiff;

                await _duelMatchRepository.Save(match);

                await ReplyAsync($"{match.LooserName} has lost against {match.WinnerName}").AutoRemoveMessage(10);

                winner.MatchHistory.Insert(0, match);
                winner.MatchHistory = winner.MatchHistory.Take(10).ToList();
                await _duelPlayerRepository.Save(winner);

                looser.MatchHistory.Insert(0, match);
                looser.MatchHistory = looser.MatchHistory.Take(10).ToList();
                await _duelPlayerRepository.Save(looser);
            }
        }

        [Command("stats")]
        [Summary("User duel stats")]
        public async Task Stats(IGuildUser user = null)
        {
            if (!PickupHelpers.IsInDuelChannel(Context.Channel)) return;

            user ??= (IGuildUser)Context.User;

            var duelPlayer = await _duelPlayerRepository.Find(user);
            if (duelPlayer == null)
            {
                await ReplyAsync($"{PickupHelpers.GetNickname(user)} is not a duellist.").AutoRemoveMessage(10);
            }
            else
            {
                var games = (await _duelMatchRepository.FindByChallengerId(user)).ToList();
                games.AddRange(await _duelMatchRepository.FindByChallengeeId(user));

                games = games.Where(w => w.Started && !string.IsNullOrEmpty(w.WinnerId)).ToList();

                var last10 = duelPlayer.MatchHistory.Select(g =>
                                           $"{(Convert.ToUInt64(g.WinnerId) == user.Id ? $":first_place: Won against {g.LooserName} MMR +{g.MMR}" : $":cry: Lost against {g.WinnerName} MMR -{g.MMR}")}")
                                        .ToArray();

                var embed = new EmbedBuilder
                {
                    Title = $"{PickupHelpers.GetNickname(user)} stats [{((SkillLevel)duelPlayer.Skill).ToString()}] MMR: {duelPlayer.MMR}",
                    Timestamp = duelPlayer.Timestamp
                };
                embed.WithFields(new List<EmbedFieldBuilder>
                {
                    new EmbedFieldBuilder { Name = ":first_place: **Wins**", Value = games.Count(w => w.WinnerId == duelPlayer.Id.ToString()), IsInline = true},
                    new EmbedFieldBuilder { Name = "\u200b", Value = "\u200b", IsInline = true},
                    new EmbedFieldBuilder { Name = ":cry: **Losses**", Value = games.Count(w => w.LooserId == duelPlayer.Id.ToString()), IsInline = true },
                    new EmbedFieldBuilder { Name = "\u200b", Value = "\u200b" },
                    new EmbedFieldBuilder
                    {
                        Name = "**Last matches**",
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
        private static int UpdateMMR(DuelPlayer winner, DuelPlayer looser)
        {
            var mmr = (int)Math.Ceiling(looser.MMR / (decimal)winner.MMR * 0.05m * looser.MMR);
            if (mmr < 10) mmr += 10;
            if (mmr > 200) mmr = 200;

            looser.MMR -= mmr;
            winner.MMR += mmr;

            return mmr;
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
