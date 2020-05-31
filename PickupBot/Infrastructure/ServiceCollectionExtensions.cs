using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PickupBot.Data.Models;
using PickupBot.Data.Repositories;

namespace PickupBot.Infrastructure
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection ConfigureSettings<TConfig>(this IServiceCollection services, IConfiguration configuration)
            where TConfig : class, new()
        {
            if (services == null) throw new ArgumentNullException(nameof(services));
            if (configuration == null) throw new ArgumentNullException(nameof(configuration));

            var config = new TConfig();
            configuration.Bind(config);
            services.AddSingleton(config);

            return services;
        }

        public static IServiceCollection AddRepositories(this IServiceCollection services, IConfiguration configuration)
        {
            var storageConnectionString = configuration.GetConnectionString("StorageConnectionString");

            services.AddScoped<IAzureTableStorage<PickupQueue>>(provider =>
                    new AzureTableStorage<PickupQueue>(
                        new AzureTableSettings(storageConnectionString, nameof(PickupQueue))
                    )
                )
                .AddScoped<IAzureTableStorage<FlaggedSubscriber>>(provider =>
                    new AzureTableStorage<FlaggedSubscriber>(
                        new AzureTableSettings(storageConnectionString, nameof(FlaggedSubscriber))
                    )
                )
                .AddScoped<IAzureTableStorage<SubscriberActivities>>(provider =>
                    new AzureTableStorage<SubscriberActivities>(
                        new AzureTableSettings(storageConnectionString, nameof(SubscriberActivities))
                    )
                )
                .AddScoped<IAzureTableStorage<DuelPlayer>>(provider =>
                    new AzureTableStorage<DuelPlayer>(
                        new AzureTableSettings(storageConnectionString, nameof(DuelPlayer))
                    )
                )
                .AddScoped<IAzureTableStorage<DuelMatch>>(provider =>
                    new AzureTableStorage<DuelMatch>(
                        new AzureTableSettings(storageConnectionString, nameof(DuelMatch))
                    )
                )
                .AddScoped<IAzureTableStorage<DuelChallenge>>(provider =>
                    new AzureTableStorage<DuelChallenge>(
                        new AzureTableSettings(storageConnectionString, nameof(DuelChallenge))
                    )
                )
                .AddScoped<IQueueRepository, PickupQueueRepository>()
                .AddScoped<IFlaggedSubscribersRepository, FlaggedSubscribersRepository>()
                .AddScoped<ISubscriberActivitiesRepository, SubscriberActivitiesRepository>()
                .AddScoped<IDuelPlayerRepository, DuelPlayerRepository>()
                .AddScoped<IDuelMatchRepository, DuelMatchRepository>()
                .AddScoped<IDuelChallengeRepository, DuelChallengeRepository>();

            return services;
        }
    }
}
