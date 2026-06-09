using SmoothingDataApi.Configuration;
using SmoothingDataApi.Models;

namespace SmoothingDataApi.Services
{
// Alpha-Beta — знает цену и скорость, поэтому лаг меньше чем у EMA.
//
// Predict:  x_pred = x + v * dt        — угадываем где цена окажется
//           v_pred = v
// Update:   residual = z - x_pred      — смотрим на ошибку прогноза
//           x = x_pred + alpha * residual
//           v = v_pred + (beta / dt) * residual
//
// Коэффициенты зависят от шага k:
//   alpha(k) = 2(2k-1) / (k(k+1))
//   beta(k)  = 6 / (k(k+1))
// В начале (малый k) — большие alpha/beta, фильтр быстро обучается.
// После k=30 коэффициенты фиксируются — фильтр выходит на стабильный режим.
    public sealed class AlphaBetaFilterService
    {
        private const double MinDtSeconds = 0.001;
        private const double MinVelocityUpdateDtSeconds = SmoothingDefaults.AlphaBeta.MinVelocityUpdateDtSeconds;
        // AutoStepFloor равен 3 для обеспечения естественного подбора коэффициентов.
        private const int AutoStepFloor = 3;
        private const double TurnVelocityDamping = (double)SmoothingDefaults.AlphaBeta.TurnVelocityDamping;
        private const double MaxVelocity = (double)SmoothingDefaults.AlphaBeta.MaxVelocity;
        private const int AutoKMax = SmoothingDefaults.AlphaBeta.AutoKMax;

        public decimal UpdateState(AlphaBetaState state, PricePoint point)
        {
            if (!state.Initialized)
            {
                state.X = (double)point.Price;
                state.V = 0;
                state.PrevTimestamp = point.Timestamp;
                state.Step = 1;
                state.Initialized = true;
                return point.Price;
            }

            var rawDt = (point.Timestamp - state.PrevTimestamp).TotalSeconds;
            if (rawDt < 0)
                return (decimal)state.X; 

            var dt = Math.Max(rawDt, MinDtSeconds);
            state.Step = Math.Clamp(state.Step + 1, AutoStepFloor, AutoKMax);

            ApplyStep(ref state.X, ref state.V, (double)point.Price, dt, state.Step);
            state.PrevTimestamp = point.Timestamp;

            return (decimal)state.X;
        }

        public IReadOnlyList<SmoothedPoint> Smooth(IReadOnlyList<PricePoint> points)
        {
            if (points.Count == 0)
                return [];

            var state = new AlphaBetaState();
            return points
                .Select(p => new SmoothedPoint(p.Timestamp, p.Price, UpdateState(state, p)))
                .ToList();
        }

        private static void ApplyStep(ref double x, ref double v, double price, double dt, int step)
        {
            var dtForVelocityUpdate = Math.Max(dt, MinVelocityUpdateDtSeconds);
            var (alpha, beta) = GetDynamicCoefficients(step);

            var xPred = x + v * dt;
            var residual = price - xPred;

            if (v != 0.0 && residual != 0.0 && Math.Sign(v) != Math.Sign(residual))
            {
                v *= TurnVelocityDamping;
                xPred = x + v * dt;
                residual = price - xPred;
            }

            var candidateX = xPred + alpha * residual;
            x = double.IsFinite(candidateX) ? candidateX : price;
            v = Math.Clamp(v + (beta / dtForVelocityUpdate) * residual, -MaxVelocity, MaxVelocity);
        }

        private static (double Alpha, double Beta) GetDynamicCoefficients(int step)
        {
            var alpha = 2.0 * (2.0 * step - 1.0) / (step * (step + 1.0));
            var beta = 6.0 / (step * (step + 1.0));
            return (alpha, beta);
        }
    }
}
