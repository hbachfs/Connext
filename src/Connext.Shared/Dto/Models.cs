namespace Connext.Shared.Dto;

public class ApiResponse<T>
{
    public bool Success { get; set; }
    public required string Message { get; set; }
    public T? Data { get; set; }

    public static ApiResponse<T> Ok(T? data, string message = "Thành công")
        => new() { Success = true, Message = message, Data = data };

    public static ApiResponse<T> Fail(string message)
        => new() { Success = false, Message = message, Data = default };
}

