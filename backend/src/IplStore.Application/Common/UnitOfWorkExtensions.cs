namespace IplStore.Application.Common;

public static class UnitOfWorkExtensions
{
    /// <summary>Convenience overload for use cases that produce no value.</summary>
    public static Task ExecuteInTransactionAsync(
        this IUnitOfWork unitOfWork,
        Func<CancellationToken, Task> work,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(work);

        return unitOfWork.ExecuteInTransactionAsync<bool>(
            async ct =>
            {
                await work(ct);
                return true;
            },
            cancellationToken);
    }
}
