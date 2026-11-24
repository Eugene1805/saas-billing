namespace Saas.Billing.Domain.Common;

public sealed record DomainError(
    string Code,
    string Message,
    int HttpStatus,
    IReadOnlyDictionary<string, object?>? Extensions = null);

public sealed class Result
{
    private Result(bool isSuccess, DomainError? error)
    {
        IsSuccess = isSuccess;
        Error = error;
    }

    public bool IsSuccess { get; }
    public DomainError? Error { get; }

    public static Result Success() => new(true, null);

    public static Result Failure(
        string code,
        string message,
        int httpStatus,
        IReadOnlyDictionary<string, object?>? extensions = null) =>
        new(false, new DomainError(code, message, httpStatus, extensions));

    public static Result Failure(DomainError error) => new(false, error);
}

public sealed class Result<T>
{
    private readonly T? _value;
    private readonly DomainError? _error;

    private Result(T value)
    {
        IsSuccess = true;
        _value = value;
        _error = null;
    }

    private Result(DomainError error)
    {
        IsSuccess = false;
        _value = default;
        _error = error;
    }

    public bool IsSuccess { get; }

    public T Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException("El resultado falló y no contiene valor.");

    public DomainError Error => !IsSuccess
        ? _error!
        : throw new InvalidOperationException("El resultado fue exitoso y no contiene error.");

    public static Result<T> Success(T value) => new(value);

    public static Result<T> Failure(
        string code,
        string message,
        int httpStatus,
        IReadOnlyDictionary<string, object?>? extensions = null) =>
        new(new DomainError(code, message, httpStatus, extensions));

    public static Result<T> Failure(DomainError error) => new(error);
}
