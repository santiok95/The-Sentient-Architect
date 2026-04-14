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

    // ── Named failure factories ────────────────────────────────────────────
    public static Result NotFound(string message)    => Failure([message], ErrorType.NotFound);
    public static Result Conflict(string message)    => Failure([message], ErrorType.Conflict);
    public static Result Forbidden(string message)   => Failure([message], ErrorType.Forbidden);
    public static Result Unauthorized(string message) => Failure([message], ErrorType.Unauthorized);

    // ── Chaining ─────────────────────────────────────────────────────────
    /// <summary>Execute <paramref name="action"/> only when the result succeeded.</summary>
    public Result OnSuccess(Action action)
    {
        if (Succeeded) action();
        return this;
    }

    /// <summary>Execute <paramref name="action"/> only when the result failed.</summary>
    public Result OnFailure(Action<List<string>> action)
    {
        if (!Succeeded) action(Errors);
        return this;
    }

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

    // ── Named failure factories ────────────────────────────────────────────
    public static Result<TData> NotFound(string message)    => Failure([message], ErrorType.NotFound);
    public static Result<TData> Conflict(string message)    => Failure([message], ErrorType.Conflict);
    public static Result<TData> Forbidden(string message)   => Failure([message], ErrorType.Forbidden);
    public static Result<TData> Unauthorized(string message) => Failure([message], ErrorType.Unauthorized);

    // ── Chaining ─────────────────────────────────────────────────────────
    /// <summary>Transform the data value when succeeded; propagate failure unchanged.</summary>
    public Result<TNext> Map<TNext>(Func<TData, TNext> mapper)
        => Succeeded
            ? Result<TNext>.SuccessWith(mapper(Data!))
            : Result<TNext>.Failure(Errors, ErrorType);

    /// <summary>Chain another operation that returns a Result; short-circuits on failure.</summary>
    public Result<TNext> Bind<TNext>(Func<TData, Result<TNext>> next)
        => Succeeded ? next(Data!) : Result<TNext>.Failure(Errors, ErrorType);

    /// <summary>Async version of <see cref="Bind{TNext}"/>.</summary>
    public async Task<Result<TNext>> BindAsync<TNext>(Func<TData, Task<Result<TNext>>> next)
        => Succeeded ? await next(Data!) : Result<TNext>.Failure(Errors, ErrorType);

    /// <summary>Execute <paramref name="action"/> only when succeeded; returns this for fluent chaining.</summary>
    public new Result<TData> OnSuccess(Action action)
    {
        if (Succeeded) action();
        return this;
    }

    /// <summary>Execute <paramref name="action"/> with the data only when succeeded.</summary>
    public Result<TData> OnSuccess(Action<TData> action)
    {
        if (Succeeded) action(Data!);
        return this;
    }

    /// <summary>Async version of <see cref="OnSuccess(Action{TData})"/>.</summary>
    public async Task<Result<TData>> OnSuccessAsync(Func<TData, Task> action)
    {
        if (Succeeded) await action(Data!);
        return this;
    }

    /// <summary>Execute <paramref name="action"/> only when failed; returns this for fluent chaining.</summary>
    public new Result<TData> OnFailure(Action<List<string>> action)
    {
        if (!Succeeded) action(Errors);
        return this;
    }

    public static implicit operator Result<TData>(List<string> errors)
        => Failure(errors);

    public static implicit operator Result<TData>(TData data)
        => SuccessWith(data);

    public static implicit operator bool(Result<TData> result)
        => result.Succeeded;
}
