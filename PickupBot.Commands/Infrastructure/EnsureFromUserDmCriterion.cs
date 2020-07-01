using System.Threading.Tasks;
using Discord;
using Discord.Addons.Interactive;
using Discord.Commands;

namespace PickupBot.Commands.Infrastructure
{
    public class EnsureFromUserDmCriterion : ICriterion<IMessage>
    {
        private readonly ulong _channelId;
        private readonly ulong _userId;
        public EnsureFromUserDmCriterion(IMessageChannel channel, IUser user)
        {
            _channelId = channel.Id;
            _userId = user.Id;
        }

        public Task<bool> JudgeAsync(SocketCommandContext sourceContext, IMessage parameter)
        {
            bool ok = (parameter.Channel.Id == _channelId && parameter.Author.Id == _userId);
            return Task.FromResult(ok);
        }
    }
}
