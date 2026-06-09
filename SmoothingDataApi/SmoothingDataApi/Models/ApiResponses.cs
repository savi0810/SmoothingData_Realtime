namespace SmoothingDataApi.Models
{
    public sealed record SmoothedPointsResponse(
        string Symbol,
        string Algorithm,
        IReadOnlyList<SmoothedPoint> Points);
}
