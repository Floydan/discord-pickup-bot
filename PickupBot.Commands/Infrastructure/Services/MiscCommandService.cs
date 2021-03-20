using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;
using PickupBot.Commands.Extensions;
using PickupBot.Commands.Infrastructure.Utilities;
using PickupBot.Data.Models;
using PickupBot.Data.Repositories;
using PickupBot.Data.Repositories.Interfaces;
using PickupBot.Encryption;

namespace PickupBot.Commands.Infrastructure.Services
{
    public class MiscCommandService : IMiscCommandService
    {
        private readonly IQueueRepository _queueRepository;
        private readonly IFlaggedSubscribersRepository _flagRepository;
        private readonly ILogger<MiscCommandService> _logger;
        private readonly IServerRepository _serverRepository;
        private readonly EncryptionSettings _encryptionSettings;
        //private readonly string _rconPassword;
        //private readonly string _rconHost;
        //private readonly int _rconPort;

        public MiscCommandService(
            IQueueRepository queueRepository, 
            IFlaggedSubscribersRepository flagRepository, 
            //PickupBotSettings pickupBotSettings, 
            ILogger<MiscCommandService> logger,
            IServerRepository serverRepository,
            EncryptionSettings encryptionSettings)
        {
            _queueRepository = queueRepository;
            _flagRepository = flagRepository;
            _logger = logger;
            _serverRepository = serverRepository;
            _encryptionSettings = encryptionSettings;

            //_rconPassword = pickupBotSettings.RCONServerPassword ?? "";
            //_rconHost = pickupBotSettings.RCONHost ?? "";
            //int.TryParse(pickupBotSettings.RCONPort ?? "0", out _rconPort);
        }

        public async Task<PickupQueue> VerifyQueueByName(string queueName, IGuildChannel channel)
        {
            var queue = await _queueRepository.FindQueue(queueName, channel.Guild.Id.ToString());

            if (queue != null) return queue;

            await ((ITextChannel)channel).SendMessageAsync($"`Queue with the name '{queueName}' doesn't exists!`").AutoRemoveMessage(10);
            return null;
        }

        public async Task<bool> VerifyUserFlaggedStatus(IGuildUser user, ISocketMessageChannel channel)
        {
            var flagged = await _flagRepository.IsFlagged(user);
            if (flagged == null) return true;

            var sb = new StringBuilder()
                .AppendLine("You have been flagged which means that you can't join or create queues.")
                .AppendLine("**Reason**")
                .AppendLine($"_{flagged.Reason}_");

            var embed = new EmbedBuilder
            {
                Title = "You are flagged",
                Description = sb.ToString(),
                Color = Color.Orange
            }.Build();

            await channel.SendMessageAsync(embed: embed)
                .AutoRemoveMessage(10);

            return false;
        }

        public void TriggerDelayedRconNotification(PickupQueue queue)
        {
            // 2 minute delay message
            AsyncUtilities.DelayAction(TimeSpan.FromMinutes(2), async t => { await TriggerRconNotification(queue); });

            // 4 minute delay message
            AsyncUtilities.DelayAction(TimeSpan.FromMinutes(4), async t => { await TriggerRconNotification(queue); });
        }

        public async Task TriggerRconNotification(PickupQueue queue)
        {
            if (!queue.Rcon) return;

            var rconPassword = "";
            var rconPort = 0;
            var rconHost = "";

            var server = await _serverRepository.Find(Convert.ToUInt64(queue.GuildId), queue.Host);
            if (server != null)
            {
                rconPort = server.Port;
                rconHost = server.Host;
                rconPassword = !string.IsNullOrWhiteSpace(server.RconPassword) ? 
                    EncryptionProvider.AESDecrypt(server.RconPassword, _encryptionSettings.Key, _encryptionSettings.IV) : 
                    string.Empty;
            }

            if (string.IsNullOrWhiteSpace(rconPassword) || string.IsNullOrWhiteSpace(rconHost) || rconPort == 0) return;

            try
            {
                var redTeam = queue.Teams.FirstOrDefault();
                var blueTeam = queue.Teams.LastOrDefault();

                var command = $"say \"^2Pickup '^3{queue.Name}^2' has started! " +
                              $"^1RED TEAM: ^5{string.Join(", ", redTeam.Subscribers.Select(w => w.Name))} ^7- " +
                              $"^4BLUE TEAM: ^5{string.Join(", ", blueTeam.Subscribers.Select(w => w.Name))}\"";

                await RCON.UDPSendCommand(command, rconHost, rconPassword, rconPort, true);

            }
            catch (Exception e)
            {
                _logger.LogError(e, e.Message);
            }
        }
    }

    public interface IMiscCommandService
    {
        void TriggerDelayedRconNotification(PickupQueue queue);
        Task TriggerRconNotification(PickupQueue queue);
        Task<PickupQueue> VerifyQueueByName(string queueName, IGuildChannel channel);
        Task<bool> VerifyUserFlaggedStatus(IGuildUser user, ISocketMessageChannel channel);
    }
}
