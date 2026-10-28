namespace ThorntonEnergy.Shared.Common;

// The ways a service call can end. Controllers turn these into HTTP status codes.
public enum ResultStatus { Success, NotFound, Unauthorized, Forbidden, Invalid, Conflict }

// What a service returns. Expected problems (not found, not allowed, bad input) are results, not exceptions:
// exceptions are for things that should never happen.
public sealed class Result<T>
{
    public ResultStatus Status { get; }
    public T? Value { get; }
    public string? Error { get; }

    public bool IsSuccess => Status == ResultStatus.Success;

    private Result(ResultStatus status, T? value, string? error)
    {
        Status = status;
        Value = value;
        Error = error;
    }

    public static Result<T> Success(T value) => new(ResultStatus.Success, value, null);
    public static Result<T> NotFound(string? error = null) => new(ResultStatus.NotFound, default, error);
    public static Result<T> Unauthorized(string error) => new(ResultStatus.Unauthorized, default, error);
    public static Result<T> Forbidden(string? error = null) => new(ResultStatus.Forbidden, default, error);
    public static Result<T> Invalid(string error) => new(ResultStatus.Invalid, default, error);
    public static Result<T> Conflict(string error) => new(ResultStatus.Conflict, default, error);
}
