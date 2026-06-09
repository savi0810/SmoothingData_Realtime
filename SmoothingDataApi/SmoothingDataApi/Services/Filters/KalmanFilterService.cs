using SmoothingDataApi.Configuration;
using SmoothingDataApi.Models;

namespace SmoothingDataApi.Services
{
// Kalman — знает цену, скорость и уровень собственной уверенности.
//
// Состояние: x (цена), v (скорость), P (матрица неопределённости 2×2)
//
// Predict:  x_pred = x + v*dt
//           P_pred = F*P*F^T + Q(dt)   — неопределённость растёт со временем
//
// Update:   innovation = z - x_pred    — ошибка прогноза
//           K = P_pred * H^T / (P00 + R) — Kalman gain: баланс между моделью и данными
//           x = x_pred + K0 * innovation
//           v = v_pred + K1 * innovation
//
// Q — шум процесса: насколько рынок непредсказуем (больше Q → быстрее реагирует).
// R — шум измерения: насколько шумные тики    (больше R → сильнее сглаживает).
// Q/R определяет характер фильтра.
    public sealed class KalmanFilterService
    {
        private const double MinDtSeconds = 0.001;
        private const double MinVariance = 1e-12;
        private const double Q = SmoothingDefaults.Kalman.ProcessNoise;
        private const double R = SmoothingDefaults.Kalman.MeasurementNoise;

        public decimal UpdateState(KalmanState state, PricePoint point)
        {
            var z = (double)point.Price;

            if (!state.Initialized)
            {
                state.X   = z;
                state.V   = 0.0;
                state.P00 = R;
                state.P01 = 0.0;
                state.P10 = 0.0;
                state.P11 = 1.0;
                state.PrevTimestamp = point.Timestamp;
                state.Initialized = true;
                return point.Price;
            }

            var rawDt = (point.Timestamp - state.PrevTimestamp).TotalSeconds;
            if (rawDt < 0)
                return (decimal)state.X; 

            ApplyStep(state, z, Math.Max(rawDt, MinDtSeconds));
            state.PrevTimestamp = point.Timestamp;

            return (decimal)state.X;
        }

        public IReadOnlyList<SmoothedPoint> Smooth(IReadOnlyList<PricePoint> points)
        {
            if (points.Count == 0)
                return [];

            var state = new KalmanState();
            return points
                .Select(p => new SmoothedPoint(p.Timestamp, p.Price, UpdateState(state, p)))
                .ToList();
        }

        private static void ApplyStep(KalmanState s, double z, double dt)
        {
            // 1) Предсказание: F = [[1, dt], [0, 1]]
            var xPred = s.X + s.V * dt;
            var vPred = s.V;

            // 2) Предсказание ковариации: P' = FPF^T + Q(dt)
            // Q(dt) = q * [[dt^3/3, dt^2/2], [dt^2/2, dt]]
            var dt2 = dt * dt;
            var dt3 = dt2 * dt;

            var p00Pred = s.P00 + dt * (s.P10 + s.P01) + dt2 * s.P11 + Q * (dt3 / 3.0);
            var p01Pred = s.P01 + dt * s.P11 + Q * (dt2 / 2.0);
            var p10Pred = s.P10 + dt * s.P11 + Q * (dt2 / 2.0);
            var p11Pred = s.P11 + Q * dt;

            // 3) Коррекция: H = [1, 0]
            var innovation = z - xPred;
            var sc = p00Pred + R;

            var k0 = p00Pred / sc;
            var k1 = p10Pred / sc;

            var candidateX = xPred + k0 * innovation;
            var candidateV = vPred + k1 * innovation;

            // Защита от NaN/Inf при экстремальных арифметических операциях
            s.X = double.IsFinite(candidateX) ? candidateX : z;
            s.V = double.IsFinite(candidateV) ? candidateV : 0.0;

            // Форма Джозефа: P = (I - KH)P'(I - KH)^T + K R K^T
            var a00 = 1.0 - k0;
            var a10 = -k1;

            var t00 = a00 * p00Pred;
            var t01 = a00 * p01Pred;
            var t10 = a10 * p00Pred + p10Pred;
            var t11 = a10 * p01Pred + p11Pred;

            var newP00 = t00 * a00 + k0 * R * k0;
            var newP01 = t00 * a10 + t01 + k0 * R * k1;
            var newP10 = t10 * a00 + k1 * R * k0;
            var newP11 = t10 * a10 + t11 + k1 * R * k1;

            var symP01 = 0.5 * (newP01 + newP10);

            s.P00 = Math.Max(newP00, MinVariance);
            s.P01 = symP01;
            s.P10 = symP01;
            s.P11 = Math.Max(newP11, MinVariance);
        }
    }
}
