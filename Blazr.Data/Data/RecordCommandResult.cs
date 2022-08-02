namespace Blazr.Data.Data
{
    public record RecordCommandResult
    {
        public bool Success { get; init; }

        public string Message { get; init; } = string.Empty;

        public static RecordCommandResult Successful()
            => new RecordCommandResult { Success = true };

        public static RecordCommandResult Failure(string message)
            => new RecordCommandResult { Success = false, Message = message };

    }
}
