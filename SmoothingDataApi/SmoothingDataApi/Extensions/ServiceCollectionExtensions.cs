using SmoothingDataApi.Services;
using SmoothingDataApi.Services.Streaming;
using System.Text.Json;

namespace SmoothingDataApi.Extensions
{
    internal static class ServiceCollectionExtensions
    {
        internal static IServiceCollection AddSignalRWithDefaults(this IServiceCollection services)
        {
            services.AddSignalR()
                .AddJsonProtocol(options =>
                {
                    options.PayloadSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
                });

            return services;
        }

        internal static IServiceCollection AddStreamingServices(this IServiceCollection services)
        {
            services.AddSingleton<InMemoryPriceBufferStore>();
            services.AddSingleton<BinancePriceStreamService>();
            services.AddSingleton<PriceBroadcastService>();
            services.AddHostedService(sp => sp.GetRequiredService<PriceBroadcastService>());

            return services;
        }

        internal static IServiceCollection AddFilterServices(this IServiceCollection services)
        {
            services.AddSingleton<ExponentialSmoothingService>();
            services.AddSingleton<AlphaBetaFilterService>();
            services.AddSingleton<KalmanFilterService>();

            return services;
        }
    }
}
