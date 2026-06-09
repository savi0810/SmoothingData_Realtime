namespace SmoothingDataApi.Configuration
{
    public static class SmoothingDefaults
    {
        public static class Broadcast
        {
            public const int WindowMinutes = 5;
            public const int MaxSmoothingPoints = 3_000;

        }

        public static class Ema
        {
            public const double HalfLifeSeconds = 3;
        }

        public static class AlphaBeta
        {
            // Чем больше AutoKMax → тем меньше alpha/beta в установившемся режиме → сильнее сглаживание.
            // k=12 → alpha≈0.295 (быстро); k=30 → alpha≈0.127 (соответствует уровню Калмана R=2500).
            public const int AutoKMax = 30;
            public const double MinVelocityUpdateDtSeconds = 0.06;
            public const decimal TurnVelocityDamping = 0.55m;
            public const decimal MaxVelocity = 100_000m;
        }

        public static class Kalman
        {
            // Q  = спектральная плотность шума процесса (дисперсия ускорения в секунду).
            //      Чем больше → фильтр быстрее реагирует на реальные изменения тренда, но пропускает больше шума.
            // R  = дисперсия шума измерений (USDT²).
            //      Типичный шум тиков BTC/USDT σ ≈ 50 USDT → R = 2 500.
            //      Увеличить R для более сильного сглаживания, уменьшить для более быстрого отклика.
            public const double ProcessNoise = 10.0;
            public const double MeasurementNoise = 2500.0;
        }
    }
}
