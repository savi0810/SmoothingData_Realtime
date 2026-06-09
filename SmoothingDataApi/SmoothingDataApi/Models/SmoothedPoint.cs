namespace SmoothingDataApi.Models
{
    public sealed record SmoothedPoint(DateTime Timestamp, decimal Raw, decimal Smoothed);
}
