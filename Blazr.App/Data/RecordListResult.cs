namespace Blazr.App.Data;

public record RecordListResult<TRecord>
{
    public IEnumerable<TRecord> Items { get; init; } = Enumerable.Empty<TRecord>();

    public bool Success { get; init; }

    public string Message { get; init; } = string.Empty;

    public static RecordListResult<TRecord> Successful(IEnumerable<TRecord> items)
        => new RecordListResult<TRecord> { Items = items, Success = true };

    public static RecordListResult<TRecord> Failure(string message)
        => new RecordListResult<TRecord> { Success = false, Message = message };
}
