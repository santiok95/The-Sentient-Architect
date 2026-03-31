namespace SentientArchitect.Application.Common.Results;

public class Result
{
    public Result(bool succeeded, List<string> errors)
    {
        Succeeded = succeeded;
        Errors = errors;
    }

    public Result() { }

    public bool Succeeded { get; set; }

    public List<string> Errors { get; set; } = [];

    public static Result Success => new(true, []);

    public static Result Failure(IEnumerable<string> errors)
        => new(false, errors.ToList());

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

    public Result(bool succeeded, TData? data, List<string> errors)
        : base(succeeded, errors)
    {
        Data = data;
    }

    public TData? Data { get; set; }

    public static Result<TData> SuccessWith(TData data)
        => new(true, data, []);

    public new static Result<TData> Failure(IEnumerable<string> errors)
        => new(false, default, errors.ToList());

    public static implicit operator Result<TData>(List<string> errors)
        => Failure(errors);

    public static implicit operator Result<TData>(TData data)
        => SuccessWith(data);

    public static implicit operator bool(Result<TData> result)
        => result.Succeeded;
}
