namespace IplStore.Application.Common;

/// <summary>The request is syntactically valid but its values are not acceptable. HTTP 400.</summary>
public sealed class RequestValidationException : Exception
{
    public RequestValidationException(string field, string error)
        : this(new Dictionary<string, string[]> { [field] = new[] { error } })
    {
    }

    public RequestValidationException(IReadOnlyDictionary<string, string[]> errors)
        : base("One or more validation errors occurred.")
    {
        Errors = errors;
    }

    public IReadOnlyDictionary<string, string[]> Errors { get; }
}

/// <summary>
/// A concurrent writer won the race (unique-key or optimistic-concurrency violation) and
/// automatic retries could not resolve it. HTTP 409 - the client may re-read and retry.
/// </summary>
public sealed class ConcurrencyConflictException : Exception
{
    public ConcurrencyConflictException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}
