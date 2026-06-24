using System.Collections.Generic;

namespace SmartTravelPlaners.BLL.DTOs.Common
{
    public class ApiResponse<T>
    {
        public bool Succeeded { get; set; }
        public string Message { get; set; }
        public T Data { get; set; }
        public List<string> Errors { get; set; } = new();

        public static ApiResponse<T> Success(T data, string message = null) =>
            new() { Succeeded = true, Data = data, Message = message };

        public static ApiResponse<T> Failure(List<string> errors, string message = null) =>
            new() { Succeeded = false, Errors = errors, Message = message };

        public static ApiResponse<T> Failure(string error, string message = null) =>
            new() { Succeeded = false, Errors = new List<string> { error }, Message = message };
    }

    public class ApiResponse
    {
        public bool Succeeded { get; set; }
        public string Message { get; set; }
        public List<string> Errors { get; set; } = new();

        public static ApiResponse Success(string message = null) =>
            new() { Succeeded = true, Message = message };

        public static ApiResponse Failure(List<string> errors, string message = null) =>
            new() { Succeeded = false, Errors = errors, Message = message };

        public static ApiResponse Failure(string error, string message = null) =>
            new() { Succeeded = false, Errors = new List<string> { error }, Message = message };
    }
}
