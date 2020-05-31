using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using PickupBot.Data.Models;

namespace PickupBot.Data.Repositories
{
    public class DuelPlayerRepository : IDuelPlayerRepository
    {
        private readonly IAzureTableStorage<DuelPlayer> _client;

        public DuelPlayerRepository(IAzureTableStorage<DuelPlayer> client)
        {
            _client = client;
        }

        public async Task<DuelPlayer> Find(IGuildUser user)
        {
            return await _client.GetItem(user.GuildId.ToString(), user.Id.ToString());
        }

        public async Task<bool> Save(IGuildUser user, SkillLevel skillLevel)
        {
            var duelPlayer = new DuelPlayer(user.GuildId, user.Id) { Name = user.Nickname ?? user.Username, Skill = (int)skillLevel };
            return await _client.InsertOrReplace(duelPlayer);
        }

        public async Task<bool> Save(DuelPlayer player)
        {
            return await _client.InsertOrMerge(player);
        }

        public async Task<bool> Delete(IGuildUser user)
        {
            return await _client.Delete(user.GuildId.ToString(), user.Id.ToString());
        }

        public async Task<IEnumerable<DuelPlayer>> List(ulong guildId)
        {
            var result = await _client.GetList(guildId.ToString());

            return result;
        }

        public async Task<IEnumerable<DuelPlayer>> List(IEnumerable<IGuildUser> users)
        {
            var guildUsers = users as IGuildUser[] ?? users.ToArray();
            if (!guildUsers.Any()) return Enumerable.Empty<DuelPlayer>();

            var result = new List<DuelPlayer>();
            foreach (var guildUser in guildUsers)
            {
                var item = await _client.GetItem(guildUser.GuildId.ToString(), guildUser.Id.ToString());
                if(item != null)
                    result.Add(item);
            }

            return result;
        }
    }
}