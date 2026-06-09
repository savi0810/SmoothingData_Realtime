namespace SmoothingDataApi.Services
{
    // Пошаговое состояние EMA для каждого символа.
    public sealed class EmaState
    {
        public double Smoothed;
        public DateTime PrevTimestamp;
        public bool Initialized;
    }

    // Пошаговое состояние фильтра Альфа-Бета для каждого символа.
    // Step отслеживает счётчик тикеров с момента инициализации, что позволяет реализовать автоматический подбор коэффициентов.
    public sealed class AlphaBetaState
    {
        public double X;
        public double V;
        public DateTime PrevTimestamp;
        public int Step;
        public bool Initialized;
    }

    // Пошаговое состояние фильтра Калмана для каждого символа.
    // Хранит полную матрицу ковариации 2×2 (p00..p11) и вектор состояния (x, v).
    public sealed class KalmanState
    {
        public double X;
        public double V;
        public double P00;
        public double P01;
        public double P10;
        public double P11;
        public DateTime PrevTimestamp;
        public bool Initialized;
    }
}
