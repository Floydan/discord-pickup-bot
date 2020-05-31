using System.Collections.Generic;
using System.Linq;
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
        
        public async Task<List<T>> GetTopListByField(string partitionKey, string propertyName, int count)
        {
            //Table
            var table = await GetTableAsync();

            //Query
            var query = new TableQuery<T>()
                .Where(TableQuery.GenerateFilterCondition("PartitionKey", QueryComparisons.Equal, partitionKey))
                .OrderBy(propertyName)
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
        
        public async Task<T> GetItemPropertyEquals(string partitionKey, string propertyName, string value)
        {
            var results =  await GetItemsPropertyEquals(partitionKey, propertyName, value);

            return results.FirstOrDefault();
        }
        
        public async Task<IEnumerable<T>> GetItemsPropertyEquals(string partitionKey, string propertyName, string value)
        {
            //Table
            var table = await GetTableAsync();
            //Query
            var query = new TableQuery<T>()
                .Where(TableQuery.CombineFilters(
                    TableQuery.GenerateFilterCondition("PartitionKey", QueryComparisons.Equal, partitionKey),
                    TableOperators.And,
                    TableQuery.GenerateFilterCondition(propertyName, QueryComparisons.Equal, value)));

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
