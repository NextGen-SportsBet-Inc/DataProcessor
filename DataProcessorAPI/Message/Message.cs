namespace Shared.Messages

{
    public record UserExistsRequest 
    {
        public string? UserId { get; set; }
    }

    public record UserExistsResponse
    {
        public bool UserIsValid { get; set; }
    }

    public record CheckAmountRequest
    {
        public string? UserId { get; set; }

    }

    public record CheckAmountResponse
    {
        public bool Error { get; set; }

        public float? Amount { get; set; }

    }

    public record RemoveAmountRequest
    {
        public string? UserId { get; set; }

        public float? AmountToRemove { get; set; }

    }

    public record RemoveAmountResponse
    {
        public bool Success { get; set; }

        public string? ErrorMessage { get; set; }

    }

    public record ChangeBetStatusRequest
    {
        public required string MatchId { get; set; }

        public required string TeamId { get; set; }
    }

    public record ChangeBetStatusResponse
    {
        public required string MatchId { get; set; }

        public required string TeamId { get; set; }
    }

}