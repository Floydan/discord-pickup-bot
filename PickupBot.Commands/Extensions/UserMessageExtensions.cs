using System;
using System.Threading;
using System.Threading.Tasks;
using Discord;
using Discord.Rest;
using PickupBot.Commands.Infrastructure.Utilities;

namespace PickupBot.Commands.Extensions
{
    public static class UserMessageExtensions
    {
        public static void AutoRemoveMessage(this IUserMessage message, int delay = 30)
        {
            if (delay <= 0 || message == null) return; //do nothing

            AsyncUtilities.DelayAction(TimeSpan.FromSeconds(delay), async _ =>
            {
                await message.DeleteAsync().ConfigureAwait(false);
            });
        }

        public static async Task<IUserMessage> AutoRemoveMessage(this Task<RestUserMessage> message, int delay = 30)
        {
            var cts = new CancellationTokenSource((delay + 1) * 1000);

            return await message.ContinueWith(a =>
            {
                a.Result.AutoRemoveMessage(delay); 
                return a.Result;
            }, cts.Token).ConfigureAwait(false);
        }

        public static async Task<IUserMessage> AutoRemoveMessage(this Task<IUserMessage> message, int delay = 30)
        {
            var cts = new CancellationTokenSource((delay + 1) * 1000);
            return await message.ContinueWith(a =>
            {
                a.Result.AutoRemoveMessage(delay); 
                return a.Result;
            }, cts.Token).ConfigureAwait(false);
        }
    }
}
