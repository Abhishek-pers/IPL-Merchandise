namespace IplStore.Application.Common;

/// <summary>
/// Transaction boundary for a use case (Unit of Work pattern).
/// </summary>
/// <remarks>
/// <para>
/// The implementation wraps <paramref name="work"/> in a database transaction AND in the
/// retry policy configured by <c>ResilienceOptions</c>. On a transient failure (dropped
/// connection, deadlock, serialization failure) the WHOLE delegate is re-executed with a
/// fresh change tracker, so <paramref name="work"/> must be re-runnable: read state inside
/// the delegate, never capture tracked entities from outside it.
/// </para>
/// <para>
/// Changes made to tracked aggregates inside the delegate are saved and committed
/// automatically when it returns. Throwing rolls everything back.
/// </para>
/// </remarks>
public interface IUnitOfWork
{
    Task<TResult> ExecuteInTransactionAsync<TResult>(
        Func<CancellationToken, Task<TResult>> work,
        CancellationToken cancellationToken);
}
