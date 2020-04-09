using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Azure.Cosmos.Table;
using PickupBot.Data.Models;

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
            return result.RequestCharge.HasValue;
        }
        
        public async Task<bool> Update(T item)
        {
            //Table
            var table = await GetTableAsync();

            //Operation
            var operation = TableOperation.Merge(item);

            //Execute
            var result = await table.ExecuteAsync(operation);
            return result.RequestCharge.HasValue;
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
            return result.RequestCharge.HasValue;
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
            return result.RequestCharge.HasValue;
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
