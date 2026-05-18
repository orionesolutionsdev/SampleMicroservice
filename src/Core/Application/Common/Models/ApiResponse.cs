namespace SampleMicroservice.Application.Common.Models;

public class ApiResponse<T>
{
    public string Message { get; set; }
    public T? Data { get; set; }

    public ApiResponse(string message, T? data = default)
    {
        Message = message;
        Data = data;
    }
}
