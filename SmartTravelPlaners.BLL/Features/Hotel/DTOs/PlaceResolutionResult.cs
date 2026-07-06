namespace SmartTravelPlaners.BLL.Features.Hotel.DTOs
{
    public enum ResolutionStatus
    {
        Resolved,
        NeedsConfirmation,
        NotFound
    }

    public class PlaceResolutionResult
    {
        public ResolutionStatus Status { get; set; }
        public string? DestId { get; set; }
        public string? DestType { get; set; }
        public string? ResolvedName { get; set; }
        public string? OriginalInput { get; set; }
        public PlaceResolutionResult? Suggestion { get; set; }
        public string? Source { get; set; } // "cache" | "api"

        public static PlaceResolutionResult Exact(string destId, string destType, string resolvedName, string source)
        {
            return new PlaceResolutionResult
            {
                Status = ResolutionStatus.Resolved,
                DestId = destId,
                DestType = destType,
                ResolvedName = resolvedName,
                Source = source
            };
        }

        public static PlaceResolutionResult NeedsConfirmation(string originalInput, string destId, string resolvedName)
        {
            return new PlaceResolutionResult
            {
                Status = ResolutionStatus.NeedsConfirmation,
                OriginalInput = originalInput,
                Suggestion = new PlaceResolutionResult
                {
                    DestId = destId,
                    ResolvedName = resolvedName
                }
            };
        }

        public static PlaceResolutionResult NotFound(string originalInput)
        {
            return new PlaceResolutionResult
            {
                Status = ResolutionStatus.NotFound,
                OriginalInput = originalInput
            };
        }
    }
}
