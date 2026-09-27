namespace IplStore.Domain.Common;

/// <summary>Tiny guard-clause helper so entity constructors stay readable.</summary>
internal static class Guard
{
    public static Guid NotEmpty(Guid value, string name) =>
        value == Guid.Empty
            ? throw new DomainException(DomainErrorCodes.InvalidArgument, $"{name} must not be empty.")
            : value;

    public static string NotBlank(string? value, string name, int maxLength = int.MaxValue)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new DomainException(DomainErrorCodes.InvalidArgument, $"{name} must not be blank.");
        }

        if (value.Length > maxLength)
        {
            throw new DomainException(DomainErrorCodes.InvalidArgument, $"{name} must be at most {maxLength} characters.");
        }

        return value;
    }

    public static int Positive(int value, string name) =>
        value <= 0
            ? throw new DomainException(DomainErrorCodes.InvalidArgument, $"{name} must be greater than zero.")
            : value;

    public static int NotNegative(int value, string name) =>
        value < 0
            ? throw new DomainException(DomainErrorCodes.InvalidArgument, $"{name} must not be negative.")
            : value;

    public static decimal NotNegative(decimal value, string name) =>
        value < 0
            ? throw new DomainException(DomainErrorCodes.InvalidArgument, $"{name} must not be negative.")
            : value;
}
