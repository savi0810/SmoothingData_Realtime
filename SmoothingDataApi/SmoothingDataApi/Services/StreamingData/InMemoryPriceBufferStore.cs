using System.Collections.Concurrent;
using SmoothingDataApi.Configuration;
using SmoothingDataApi.Models;

namespace SmoothingDataApi.Services.Streaming
{
    public sealed class InMemoryPriceBufferStore
    {
        private readonly ConcurrentDictionary<string, SymbolPriceBuffer> _buffers = new();
        private readonly TimeSpan _bucketSize;
        private readonly int _capacity;

        public InMemoryPriceBufferStore(IConfiguration configuration)
        {
            var bucketMs = Math.Max(50,
                configuration.GetValue<int?>("Broadcast:IntervalMs") ?? 500);

            _bucketSize = TimeSpan.FromMilliseconds(bucketMs);

            var windowMs = SmoothingDefaults.Broadcast.WindowMinutes * 60_000;
            _capacity = Math.Max(1000, windowMs / bucketMs * 2);
        }

        public void Add(string symbol, PricePoint point)
        {
            var buffer = _buffers.GetOrAdd(symbol, _ => new SymbolPriceBuffer(_bucketSize, _capacity));
            buffer.Add(point);
        }

        public IReadOnlyList<PricePoint> GetPointsSince(string symbol, DateTime since)
        {
            return _buffers.TryGetValue(symbol, out var buffer)
                ? buffer.GetSince(since)
                : [];
        }

        public PricePoint? GetLatest(string symbol)
        {
            return _buffers.TryGetValue(symbol, out var buffer)
                ? buffer.GetLatest()
                : null;
        }

        private sealed class SymbolPriceBuffer(TimeSpan bucketSize, int capacity)
        {
            private readonly PricePoint[] _ring = new PricePoint[capacity];
            private int _head = 0;
            private int _count = 0;
            private readonly object _lock = new();
            private DateTime _lastBucketTime = DateTime.MinValue;
            private PricePoint? _currentBucketPoint;

            private decimal _bucketPriceSum = 0m;
            private int _bucketTickCount = 0;
            private DateTime _bucketLatestTimestamp;

            public void Add(PricePoint point)
            {
                lock (_lock)
                {
                    var bucketTime = new DateTime(
                        (point.Timestamp.Ticks / bucketSize.Ticks) * bucketSize.Ticks,
                        DateTimeKind.Utc);

                    if (bucketTime > _lastBucketTime)
                    {
                        if (_bucketTickCount > 0)
                        {
                            var avgPrice = _bucketPriceSum / _bucketTickCount;
                            WriteToRing(new PricePoint(_bucketLatestTimestamp, avgPrice));
                        }

                        _lastBucketTime = bucketTime;
                        _bucketPriceSum = 0m;
                        _bucketTickCount = 0;
                    }

                    _bucketPriceSum += point.Price;
                    _bucketTickCount++;
                    _bucketLatestTimestamp = point.Timestamp;

                    var currentAvg = _bucketPriceSum / _bucketTickCount;
                    _currentBucketPoint = new PricePoint(_bucketLatestTimestamp, currentAvg);
                }
            }

            public IReadOnlyList<PricePoint> GetSince(DateTime since)
            {
                lock (_lock)
                {
                    var result = new List<PricePoint>(_count);

                    for (var i = 0; i < _count; i++)
                    {
                        var p = _ring[(_head + i) % capacity];
                        if (p.Timestamp >= since)
                            result.Add(p);
                    }

                    if (_currentBucketPoint is not null && _currentBucketPoint.Timestamp >= since)
                    {
                        if (result.Count == 0 || result[^1].Timestamp < _currentBucketPoint.Timestamp)
                            result.Add(_currentBucketPoint);
                    }

                    return result;
                }
            }

            public PricePoint? GetLatest()
            {
                lock (_lock) { return _currentBucketPoint; }
            }

            private void WriteToRing(PricePoint point)
            {
                var writeIdx = (_head + _count) % capacity;
                _ring[writeIdx] = point;
                if (_count < capacity)
                    _count++;
                else
                    _head = (_head + 1) % capacity;
            }
        }
    }
}
