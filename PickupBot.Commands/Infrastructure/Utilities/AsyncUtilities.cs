using System;
using System.Threading;
using System.Threading.Tasks;

namespace PickupBot.Commands.Infrastructure.Utilities
{
    public static class AsyncUtilities
    {
        public static void DelayAction(TimeSpan delay, Func<Task, Task> action)
        {
            var cancellationTokenSource = new CancellationTokenSource();
            var cancellationToken = cancellationTokenSource.Token;
            Task.Delay(delay, cancellationToken).ContinueWith(action, cancellationToken);
        }
    }
}
