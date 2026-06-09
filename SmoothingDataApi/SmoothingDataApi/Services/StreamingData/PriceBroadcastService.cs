using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;
using SmoothingDataApi.Hubs;
using SmoothingDataApi.Models;

namespace SmoothingDataApi.Services.Streaming
{
    public sealed class PriceBroadcastService : BackgroundService
    {
        private readonly ConcurrentDictionary<string, byte> _pending = new();
        private readonly ConcurrentDictionary<string, (decimal Raw, decimal Smoothed)> _lastSentByGroup = new(StringComparer.Ordinal);

        private readonly Dictionary<string, EmaState> _emaStates = new(StringComparer.Ordinal);
        private readonly Dictionary<string, AlphaBetaState> _alphaBetaStates = new(StringComparer.Ordinal);
        private readonly Dictionary<string, KalmanState> _kalmanStates = new(StringComparer.Ordinal);

        private readonly IHubContext<PricesHub> _hubContext;
        private readonly InMemoryPriceBufferStore _bufferStore;
        private readonly ExponentialSmoothingService _ema;
        private readonly AlphaBetaFilterService _alphaBeta;
        private readonly KalmanFilterService _kalman;
        private readonly TimeSpan _interval;
        private readonly decimal _duplicateAbsEpsilon;
        private readonly decimal _duplicateRelEpsilon;

        public PriceBroadcastService(
            IHubContext<PricesHub> hubContext,
            InMemoryPriceBufferStore bufferStore,
            ExponentialSmoothingService ema,
            AlphaBetaFilterService alphaBeta,
            KalmanFilterService kalman,
            IConfiguration configuration)
        {
            _hubContext = hubContext;
            _bufferStore = bufferStore;
            _ema = ema;
            _alphaBeta = alphaBeta;
            _kalman = kalman;
            _interval= TimeSpan.FromMilliseconds(configuration.GetValue<int>("Broadcast:IntervalMs", 500));
            _duplicateAbsEpsilon = Math.Max(0m, configuration.GetValue<decimal?>("Broadcast:DuplicateAbsEpsilon") ?? 0.01m);
            _duplicateRelEpsilon = Math.Max(0m, configuration.GetValue<decimal?>("Broadcast:DuplicateRelEpsilon") ?? 0.0000001m);
        }

        public void Enqueue(string symbol) => _pending[symbol] = 0;

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            using var timer = new PeriodicTimer(_interval);
            try
            {
                while (await timer.WaitForNextTickAsync(stoppingToken))
                    await FlushAsync(stoppingToken);
            }
            catch (OperationCanceledException) { }

            using var finalCts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            await FlushAsync(finalCts.Token);
        }

        private async Task FlushAsync(CancellationToken cancellationToken)
        {
            foreach (var symbol in _pending.Keys)
            {
                if (cancellationToken.IsCancellationRequested) break;
                if (!_pending.TryRemove(symbol, out _)) continue;

                try
                {
                    await BroadcastAsync(symbol, cancellationToken);
                }
                catch (OperationCanceledException) { }
                catch (Exception) { }
            }
        }

        private async Task BroadcastAsync(string symbol, CancellationToken cancellationToken)
        {
            var latest = _bufferStore.GetLatest(symbol);
            if (latest is null) return;

            if (!_emaStates.TryGetValue(symbol, out var emaState))
                _emaStates[symbol] = emaState = new EmaState();

            if (!_alphaBetaStates.TryGetValue(symbol, out var abState))
                _alphaBetaStates[symbol] = abState = new AlphaBetaState();

            if (!_kalmanStates.TryGetValue(symbol, out var kalmanState))
                _kalmanStates[symbol] = kalmanState = new KalmanState();

            await SendAsync(symbol, "ema", _ema.UpdateState(emaState, latest), latest, cancellationToken);
            await SendAsync(symbol, "alphabeta", _alphaBeta.UpdateState(abState, latest),  latest, cancellationToken);
            await SendAsync(symbol, "kalman", _kalman.UpdateState(kalmanState, latest), latest, cancellationToken);
        }

        private async Task SendAsync(string symbol, string method, decimal smoothed, PricePoint latest, CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested) return;

            var groupName = $"prices:{symbol}:{method}";
            var point = new SmoothedPoint(latest.Timestamp, latest.Price, smoothed);
            if (!ShouldBroadcast(groupName, point)) return;

            await _hubContext.Clients.Group(groupName).SendAsync("priceUpdated", point, cancellationToken);
        }

        private bool ShouldBroadcast(string groupName, SmoothedPoint point)
        {
            if (_lastSentByGroup.TryGetValue(groupName, out var previous) &&
                NearlyEqual(point.Raw, previous.Raw) &&
                NearlyEqual(point.Smoothed, previous.Smoothed))
                return false;

            _lastSentByGroup[groupName] = (point.Raw, point.Smoothed);
            return true;
        }

        private bool NearlyEqual(decimal current, decimal previous)
        {
            var diff = Math.Abs(current - previous);
            var scale = Math.Max(Math.Abs(current), Math.Abs(previous));
            return diff <= Math.Max(_duplicateAbsEpsilon, scale * _duplicateRelEpsilon);
        }
    }
}
