﻿using System.Collections.Generic;
using System.Threading.Tasks;
using PickupBot.Data.Models;
using PickupBot.Data.Repositories.Interfaces;

namespace PickupBot.Data.Repositories
{
    public class PickupQueueRepository : IQueueRepository
    {
        private readonly IAzureTableStorage<PickupQueue> _client;

        public PickupQueueRepository(IAzureTableStorage<PickupQueue> client)
        {
            _client = client;
        }

        public async Task<bool> AddQueue(PickupQueue queue)
        {
            queue.Name = queue.Name.ToLowerInvariant();

            return await _client.Insert(queue);
        }

        public async Task<bool> RemoveQueue(string queueName, string guildId)
        {
            var result = await _client.Delete(guildId, queueName);
            return result;
        }

        public async Task<bool> RemoveQueue(PickupQueue queue)
        {
            if (queue == null) return false;
            if (string.IsNullOrWhiteSpace(queue.PartitionKey) || string.IsNullOrWhiteSpace(queue.RowKey)) return false;
            var result = await _client.Delete(queue);
            return result;
        }

        public async Task<bool> UpdateQueue(PickupQueue queue)
        {
            return await _client.Update(queue);
        }

        public async Task<PickupQueue> FindQueue(string queueName, string guildId)
        {
            return await _client.GetItem(guildId, queueName.ToLowerInvariant());
        }

        public async Task<PickupQueue> FindQueueByMessageId(ulong messageId, string guildId)
        {
            return await _client.GetItemPropertyEquals(guildId, messageId.ToString(), nameof(PickupQueue.StaticMessageId));
        }

        public async Task<IEnumerable<PickupQueue>> AllQueues(string guildId)
        {
            return await _client.GetList(guildId);
        }
    }
}
