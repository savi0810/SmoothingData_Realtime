using Microsoft.AspNetCore.SignalR;
using SmoothingDataApi.Configuration;
using SmoothingDataApi.Models;
using SmoothingDataApi.Services;
using SmoothingDataApi.Services.Streaming;

namespace SmoothingDataApi.Hubs
{
    public sealed class PricesHub : Hub
    {
        private static readonly TimeSpan QueryWindow = TimeSpan.FromMinutes(SmoothingDefaults.Broadcast.WindowMinutes);

        private readonly BinancePriceStreamService _streamService;
        private readonly InMemoryPriceBufferStore _bufferStore;
        private readonly ExponentialSmoothingService _ema;
        private readonly AlphaBetaFilterService _alphaBeta;
        private readonly KalmanFilterService _kalman;

        public PricesHub(
            BinancePriceStreamService streamService,
            InMemoryPriceBufferStore bufferStore,
            ExponentialSmoothingService ema,
            AlphaBetaFilterService alphaBeta,
            KalmanFilterService kalman)
        {
            _streamService = streamService;
            _bufferStore = bufferStore;
            _ema = ema;
            _alphaBeta = alphaBeta;
            _kalman = kalman;
        }

        public async Task Subscribe(SubscriptionRequest request)
        {
            var symbol = NormalizeSymbol(request.Symbol);
            var method = ResolveMethod(request.Method);
            var groupName = $"prices:{symbol}:{method}";

            await Groups.AddToGroupAsync(Context.ConnectionId, groupName, Context.ConnectionAborted);

            var raw = _bufferStore.GetPointsSince(symbol, DateTime.UtcNow - QueryWindow);
            if (raw.Count > SmoothingDefaults.Broadcast.MaxSmoothingPoints)
                raw = raw.TakeLast(SmoothingDefaults.Broadcast.MaxSmoothingPoints).ToList();
            var points = Smooth(method, raw);

            await Clients.Caller.SendAsync("snapshotLoaded",
                new SmoothedPointsResponse(symbol, method, points),
                Context.ConnectionAborted);

            try
            {
                await _streamService.TrackSymbolAsync(symbol, Context.ConnectionAborted);
            }
            catch (InvalidOperationException ex)
            {
                await Clients.Caller.SendAsync("streamError", ex.Message, Context.ConnectionAborted);
            }
        }

        public async Task Unsubscribe(SubscriptionRequest request)
        {
            var symbol = NormalizeSymbol(request.Symbol);
            var method = ResolveMethod(request.Method);
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"prices:{symbol}:{method}");
        }

        private IReadOnlyList<SmoothedPoint> Smooth(string method, IReadOnlyList<PricePoint> points) =>
            method switch
            {
                "ema" => _ema.Smooth(points),
                "alphabeta" => _alphaBeta.Smooth(points),
                _ => _kalman.Smooth(points)
            };

        private static string NormalizeSymbol(string symbol)
        {
            if (string.IsNullOrWhiteSpace(symbol))
                throw new ArgumentException("Символ не может быть пустым.", nameof(symbol));

            if (symbol.Contains('/') || symbol.Contains("%2F", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Символ должен быть в формате Binance, например 'BTCUSDT' (без символа '/').", nameof(symbol));

            if (symbol.Any(char.IsWhiteSpace))
                throw new ArgumentException("Символ не должен содержать пробелы.", nameof(symbol));

            return symbol.Trim().ToUpperInvariant();
        }

        private static string ResolveMethod(string method)
        {
            if (string.IsNullOrWhiteSpace(method))
                throw new ArgumentException("Метод сглаживания не может быть пустым.", nameof(method));

            return method.Trim().ToLowerInvariant() switch
            {
                "ema" => "ema",
                "alphabeta" or "alpha-beta" or "alpha_beta" => "alphabeta",
                "kalman" => "kalman",
                var m => throw new ArgumentException($"Неизвестный метод сглаживания: '{m}'. Допустимые значения: ema, alphabeta, kalman.", nameof(method))
            };
        }
    }
}
