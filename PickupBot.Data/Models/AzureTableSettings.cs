using System;

namespace PickupBot.Data.Models
{
    public class AzureTableSettings  
    {  
        public AzureTableSettings(
            string connectionString, 
            string tableName)  
        {  
            if(string.IsNullOrWhiteSpace(connectionString))
                throw new ArgumentNullException(nameof(connectionString));
      
            if (string.IsNullOrEmpty(tableName))
                throw new ArgumentNullException(nameof(tableName));

            ConnectionString = connectionString;
            TableName = tableName;
        }  
        public string TableName { get; }
        public string ConnectionString { get; }
    }  

}
