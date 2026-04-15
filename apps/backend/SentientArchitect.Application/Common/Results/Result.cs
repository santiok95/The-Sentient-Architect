namespace SentientArchitect.Application.Common.Results;

public enum ErrorType
{
    Validation,
    NotFound,
    Conflict,
    Unauthorized,
    Forbidden,
    Failure
}

public class Result
{
    public Result(bool succeeded, ErrorType errorType, List<string> errors)
    {
        Succeeded = succeeded;
        ErrorType = errorType;
        Errors = errors;
    }

    public Result() { }

    public bool Succeeded { get; set; }
    public ErrorType ErrorType { get; set; }

    public List<string> Errors { get; set; } = [];

    public static Result Success => new(true, ErrorType.Validation, []);

    public static Result Failure(IEnumerable<string> errors, ErrorType type = ErrorType.Validation)
        => new(false, type, errors.ToList());

    public static implicit operator Result(string error)
        => Failure([error]);

    public static implicit operator Result(bool success)
        => success ? Success : Failure(["Operación fallida."]);

    public static implicit operator bool(Result result)
        => result.Succeeded;
}

public class Result<TData> : Result
{
    public Result() { }

    public Result(bool succeeded, ErrorType errorType, TData? data, List<string> errors)
        : base(succeeded, errorType, errors)
    {
        Data = data;
    }

    public TData? Data { get; set; }

    public static Result<TData> SuccessWith(TData data)
        => new(true, ErrorType.Validation, data, []);

    public new static Result<TData> Failure(IEnumerable<string> errors, ErrorType type = ErrorType.Validation)
        => new(false, type, default, errors.ToList());

    public static implicit operator Result<TData>(List<string> errors)
        => Failure(errors);

    public static implicit operator Result<TData>(TData data)
        => SuccessWith(data);

    public static implicit operator bool(Result<TData> result)
        => result.Succeeded;
}
