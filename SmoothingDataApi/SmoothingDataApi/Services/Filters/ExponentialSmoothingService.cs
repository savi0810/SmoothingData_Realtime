using SmoothingDataApi.Configuration;
using SmoothingDataApi.Models;

namespace SmoothingDataApi.Services
{
// Exponential Moving Average — знает только текущую цену.
//
// S = alpha * X + (1 - alpha) * S_prev
//   alpha = 1 - exp(-ln(2) * dt / HalfLife)
//
// alpha зависит от реального времени между тиками (dt):
//   dt → 0  =>  alpha → 0  (тик только что был, почти не меняем)
//   dt → ∞  =>  alpha → 1  (давно не было тиков, принимаем новую цену)
//
// HalfLife = 3 сек: через каждые 3 сек вес старых данных уменьшается вдвое.
    public sealed class ExponentialSmoothingService
    {
        private const double MinDtSeconds = 0.001;
        private const double MinAlpha = 0.001;
        private const double MaxAlpha = 0.999;
        private const double Ln2 = 0.6931471805599453;
        private const double HalfLife = SmoothingDefaults.Ema.HalfLifeSeconds;

        public decimal UpdateState(EmaState state, PricePoint point)
        {
            if (!state.Initialized)
            {
                state.Smoothed = (double)point.Price;
                state.PrevTimestamp = point.Timestamp;
                state.Initialized = true;
                return point.Price;
            }

            var rawDt = (point.Timestamp - state.PrevTimestamp).TotalSeconds;
            if (rawDt < 0)
                return (decimal)state.Smoothed; 

            var dt = Math.Max(rawDt, MinDtSeconds);
            state.Smoothed = ApplyStep(state.Smoothed, (double)point.Price, dt);
            state.PrevTimestamp = point.Timestamp;

            return (decimal)state.Smoothed;
        }

        public IReadOnlyList<SmoothedPoint> Smooth(IReadOnlyList<PricePoint> points)
        {
            if (points.Count == 0)
                return [];

            var state = new EmaState();
            return points
                .Select(p => new SmoothedPoint(p.Timestamp, p.Price, UpdateState(state, p)))
                .ToList();
        }

        // alpha = 1 - exp(-ln2 * dt / halfLife)
        // S = alpha * price + (1 - alpha) * prevSmoothed
        private static double ApplyStep(double prevSmoothed, double price, double dt)
        {
            var alpha = Math.Clamp(1.0 - Math.Exp(-Ln2 * dt / HalfLife), MinAlpha, MaxAlpha);
            var candidate = alpha * price + (1.0 - alpha) * prevSmoothed;
            return double.IsFinite(candidate) ? candidate : price;
        }
    }
}
