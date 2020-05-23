using Discord;
using PickupBot.Commands.Extensions;

namespace PickupBot.Commands.Infrastructure.Utilities
{
    public static class BotMessageHelper
    {
        public static void AutoRemoveMessage(IUserMessage message, int delay = 30)
        {
            message.AutoRemoveMessage(delay);
        }
    }
}
