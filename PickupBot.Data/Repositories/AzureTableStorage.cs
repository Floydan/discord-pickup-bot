using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Azure.Cosmos.Table;
using PickupBot.Data.Infrastructure.Extensions;
using PickupBot.Data.Models;
using PickupBot.Data.Repositories.Interfaces;

namespace PickupBot.Data.Repositories
{
    public class AzureTableStorage<T> : IAzureTableStorage<T> where T : TableEntity, new()
    {
        private readonly AzureTableSettings _settings;

        public AzureTableStorage(AzureTableSettings settings)
        {
            _settings = settings;
        }

        public async Task<List<T>> GetList()
        {
            //Table
            var table = await GetTableAsync();

            //Query
            var query = new TableQuery<T>();

            var results = new List<T>();
            TableContinuationToken continuationToken = null;
            do
            {
                var queryResults = await table.ExecuteQuerySegmentedAsync(query, continuationToken);

                continuationToken = queryResults.ContinuationToken;
                results.AddRange(queryResults.Results);

            } while (continuationToken != null);

            return results;
        }

        public async Task<List<T>> GetList(string partitionKey)
        {
            //Table
            var table = await GetTableAsync();

            //Query
            var query = new TableQuery<T>()
                .Where(TableQuery.GenerateFilterCondition("PartitionKey", QueryComparisons.Equal, partitionKey));

            var results = new List<T>();
            TableContinuationToken continuationToken = null;
            do
            {
                var queryResults = await table.ExecuteQuerySegmentedAsync(query, continuationToken);

                continuationToken = queryResults.ContinuationToken;

                results.AddRange(queryResults.Results);

            } while (continuationToken != null);

            return results;
        }

        /// <summary>
        /// Cosmos tables only! 
        /// </summary>
        /// <param name="partitionKey">partition key</param>
        /// <param name="propertyName">property name</param>
        /// <param name="count">number of entries to return</param>
        /// <returns>List of T</returns>
        public async Task<List<T>> GetTopListByField(string partitionKey, string propertyName, int count)
        {
            //Table
            var table = await GetTableAsync();

            //Query
            var query = new TableQuery<T>()
                .Where(TableQuery.GenerateFilterCondition("PartitionKey", QueryComparisons.Equal, partitionKey))
                .OrderByDesc(propertyName)
                .Take(count);

            var results = new List<T>();
            TableContinuationToken continuationToken = null;
            do
            {
                var queryResults = await table.ExecuteQuerySegmentedAsync(query, continuationToken);

                continuationToken = queryResults.ContinuationToken;

                results.AddRange(queryResults.Results);

            } while (continuationToken != null);

            return results;
        }

        public async Task<T> GetItemPropertyEquals(string partitionKey, string value, string propertyName)
        {
            var results = await GetItemsPropertyEquals(partitionKey, value, propertyName);

            return results.FirstOrDefault();
        }

        public async Task<IEnumerable<T>> GetItemsPropertyEquals(string partitionKey, string value, params string[] propertyNames)
        {
            //Table
            var table = await GetTableAsync();
            //Query
            var query = new TableQuery<T>()
                .Where(TableQuery.GenerateFilterCondition("PartitionKey", QueryComparisons.Equal, partitionKey));

            var ors = new TableQuery<T>();
            foreach (var propertyName in propertyNames)
            {
                ors.OrWhere(TableQuery.GenerateFilterCondition(propertyName, QueryComparisons.Equal, value));
            }

            query.AndWhere(ors.FilterString);

            var results = new List<T>();
            TableContinuationToken continuationToken = null;
            do
            {
                var queryResults = await table.ExecuteQuerySegmentedAsync(query, continuationToken);

                continuationToken = queryResults.ContinuationToken;

                results.AddRange(queryResults.Results);

            } while (continuationToken != null);

            return results;
        }

        public async Task<T> GetItem(string partitionKey, string rowKey)
        {
            //Table
            var table = await GetTableAsync();

            //Operation
            var operation = TableOperation.Retrieve<T>(partitionKey, rowKey);

            //Execute
            var result = await table.ExecuteAsync(operation);

            return (T)result.Result;
        }

        public async Task<bool> Insert(T item)
        {
            //Table
            var table = await GetTableAsync();

            //Operation
            var operation = TableOperation.Insert(item);

            //Execute
            var result = await table.ExecuteAsync(operation);
            return result.Result is T;
        }

        public async Task<bool> InsertOrReplace(T item)
        {
            //Table
            var table = await GetTableAsync();

            //Operation
            var operation = TableOperation.InsertOrReplace(item);

            //Execute
            var result = await table.ExecuteAsync(operation);
            return result.Result is T;
        }

        public async Task<bool> InsertOrMerge(T item)
        {
            //Table
            var table = await GetTableAsync();

            //Operation
            var operation = TableOperation.InsertOrMerge(item);

            //Execute
            var result = await table.ExecuteAsync(operation);
            return result.Result is T;
        }

        public async Task<bool> Update(T item)
        {
            //Table
            var table = await GetTableAsync();

            //Operation
            var operation = TableOperation.Replace(item);

            //Execute
            var result = await table.ExecuteAsync(operation);
            return result.Result is T;
        }

        public async Task<bool> Delete(string partitionKey, string rowKey)
        {
            //Item
            var item = await GetItem(partitionKey, rowKey);

            //Table
            var table = await GetTableAsync();

            //Operation
            var operation = TableOperation.Delete(item);

            //Execute
            var result = await table.ExecuteAsync(operation);
            return result.Result is T;
        }

        public async Task<bool> Delete(T item)
        {
            if (item == null) return false;

            //Table
            var table = await GetTableAsync();

            //Operation
            var operation = TableOperation.Delete(item);

            //Execute
            var result = await table.ExecuteAsync(operation);
            return result.Result is T;
        }

        private async Task<CloudTable> GetTableAsync()
        {
            //Account
            var storageAccount = CloudStorageAccount.Parse(_settings.ConnectionString);

            //Client
            var tableClient = storageAccount.CreateCloudTableClient();

            //Table
            var table = tableClient.GetTableReference(_settings.TableName);
            await table.CreateIfNotExistsAsync();

            return table;
        }
    }
}
