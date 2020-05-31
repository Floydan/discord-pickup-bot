using Microsoft.Azure.Cosmos.Table;

namespace PickupBot.Data.Infrastructure.Extensions
{
    public static class TableQueryExtensions
    {
        public static TableQuery<TElement> AndWhere<TElement>(this TableQuery<TElement> query, string filter)
        {
            query.FilterString = string.IsNullOrEmpty(query.FilterString) ?
                filter :
                TableQuery.CombineFilters(query.FilterString, TableOperators.And, filter);
            return query;
        }

        public static TableQuery<TElement> OrWhere<TElement>(this TableQuery<TElement> query, string filter)
        {
            query.FilterString = string.IsNullOrEmpty(query.FilterString) ?
                filter :
                query.FilterString = TableQuery.CombineFilters(query.FilterString, TableOperators.Or, filter);
            return query;
        }

        public static TableQuery<TElement> NotWhere<TElement>(this TableQuery<TElement> query, string filter)
        {
            query.FilterString = string.IsNullOrEmpty(query.FilterString) ?
                filter :
                query.FilterString = TableQuery.CombineFilters(query.FilterString, TableOperators.Not, filter);
            return query;
        }
    }
}
