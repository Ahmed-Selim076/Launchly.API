namespace Launchly.API.Common;

public class Result<T>
{
    public bool IsSuccess { get; private set; }
    public T? Value { get; private set; }
    public string? Error { get; private set; }
    public Dictionary<string, string[]>? ValidationErrors { get; private set; }
    public int StatusCode { get; private set; }

    private Result() { }

    public static Result<T> Success(T value) =>
        new() { IsSuccess = true, Value = value, StatusCode = 200 };

    public static Result<T> Created(T value) =>
        new() { IsSuccess = true, Value = value, StatusCode = 201 };

    public static Result<T> Failure(string error, int statusCode = 400) =>
        new() { IsSuccess = false, Error = error, StatusCode = statusCode };

    public static Result<T> NotFound(string error = "Resource not found.") =>
        new() { IsSuccess = false, Error = error, StatusCode = 404 };

    public static Result<T> Unauthorized(string error = "Unauthorized.") =>
        new() { IsSuccess = false, Error = error, StatusCode = 401 };

    public static Result<T> Forbidden(string error = "Access denied.") =>
        new() { IsSuccess = false, Error = error, StatusCode = 403 };

    public static Result<T> ValidationFailed(Dictionary<string, string[]> errors) =>
        new() { IsSuccess = false, ValidationErrors = errors, StatusCode = 422 };
}

public class ApiResponse<T>
{
    public bool Success { get; init; }
    public T? Data { get; init; }
    public string? Message { get; init; }
    public Dictionary<string, string[]>? Errors { get; init; }

    public static ApiResponse<T> Ok(T data) =>
        new() { Success = true, Data = data };

    public static ApiResponse<T> Fail(string message, Dictionary<string, string[]>? errors = null) =>
        new() { Success = false, Message = message, Errors = errors };
}

public class PagedResult<T>
{
    public IReadOnlyList<T> Items { get; init; } = [];
    public int TotalCount { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
    public bool HasNextPage => Page < TotalPages;
    public bool HasPreviousPage => Page > 1;
}