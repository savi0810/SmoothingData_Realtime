using Binance.Net.Clients;
using SmoothingDataApi.Models;

namespace SmoothingDataApi.Services.Streaming
{
    public sealed class BinancePriceStreamService : IDisposable
    {
        private readonly BinanceSocketClient _socketClient;
        private readonly InMemoryPriceBufferStore _priceBufferStore;
        private readonly PriceBroadcastService _broadcastService;
        private readonly object _lock = new();
        private readonly HashSet<string> _trackedSymbols = [];
        private readonly HashSet<string> _pendingSymbols = [];
        private readonly int _subscribeRetries;
        private readonly TimeSpan _subscribeRetryDelay;
        private readonly int _maxTrackedSymbols;

        public BinancePriceStreamService(
            InMemoryPriceBufferStore priceBufferStore,
            PriceBroadcastService broadcastService,
            IConfiguration configuration)
        {
            _priceBufferStore = priceBufferStore;
            _broadcastService = broadcastService;
            _subscribeRetries = Math.Max(0, configuration.GetValue<int?>("Binance:SubscribeRetries") ?? 2);
            _subscribeRetryDelay = TimeSpan.FromMilliseconds(Math.Max(100, configuration.GetValue<int?>("Binance:SubscribeRetryDelayMs") ?? 700));
            _maxTrackedSymbols = Math.Max(1, configuration.GetValue<int?>("Binance:MaxTrackedSymbols") ?? 50);

            var reconnectSeconds = Math.Max(1, configuration.GetValue<int?>("Binance:ReconnectIntervalSeconds") ?? 5);
            _socketClient = new BinanceSocketClient(opts =>
                opts.ReconnectInterval = TimeSpan.FromSeconds(reconnectSeconds));
        }

        public async Task<bool> TrackSymbolAsync(string symbol, CancellationToken cancellationToken = default)
        {
            symbol = symbol.ToUpperInvariant();

            lock (_lock)
            {
                if (_trackedSymbols.Contains(symbol) || _pendingSymbols.Contains(symbol))
                    return false;

                if (_trackedSymbols.Count >= _maxTrackedSymbols)
                    throw new InvalidOperationException(
                        $"Достигнут лимит отслеживаемых символов: максимум {_maxTrackedSymbols}.");

                _pendingSymbols.Add(symbol);
            }

            var success = false;
            try
            {
                for (var attempt = 0; attempt <= _subscribeRetries; attempt++)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var result = await _socketClient.SpotApi.ExchangeData.SubscribeToTradeUpdatesAsync(symbol, update =>
                    {
                        var timestamp = update.Data.TradeTime != default
                            ? update.Data.TradeTime
                            : (update.Data.EventTime != default ? update.Data.EventTime : DateTime.UtcNow);

                        _priceBufferStore.Add(symbol, new PricePoint(timestamp.ToUniversalTime(), update.Data.Price));
                        _broadcastService.Enqueue(symbol);
                    });

                    if (result.Success)
                    {
                        success = true;
                        return true;
                    }

                    var details = result.Error?.ToString() ?? "Детали ошибки недоступны.";

                    if (attempt < _subscribeRetries)
                    {
                        await Task.Delay(_subscribeRetryDelay, cancellationToken);
                        continue;
                    }

                    throw new InvalidOperationException(
                        $"Не удалось подписаться на символ '{symbol}' после {_subscribeRetries + 1} попыток. " +
                        $"Детали: {details}. Символ может быть недопустимым или сервис Binance недоступен.");
                }
            }
            finally
            {
                lock (_lock)
                {
                    _pendingSymbols.Remove(symbol);
                    if (success)
                        _trackedSymbols.Add(symbol);
                }
            }

            return false;
        }

        public void Dispose() => _socketClient.Dispose();
    }
}

