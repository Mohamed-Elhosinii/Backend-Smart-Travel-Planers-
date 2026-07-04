using System.Net;
using System.Text.Json;
using SmartTravelPlaners.BLL.DTOs.Common;

namespace SmartTravelPlaners.PL.Middlewares
{
    public class GlobalExceptionMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<GlobalExceptionMiddleware> _logger;

        public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
                await HandleExceptionAsync(context, ex, _logger);
            }
        }

        private Task HandleExceptionAsync(HttpContext context, Exception exception, ILogger logger)
        {
            var httpMethod = context.Request.Method;
            var requestPath = context.Request.Path;

            logger.LogError(exception,
                "Unhandled exception occurred. Method: {HttpMethod}, Path: {RequestPath}, ExceptionType: {ExceptionType}, Message: {ExceptionMessage}",
                httpMethod, requestPath, exception.GetType().Name, exception.Message);

            context.Response.ContentType = "application/json";
            context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;

            var response = ApiResponse<object>.Failure(
                exception.Message, 
                "An unexpected error occurred on the server."
            );

            var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
            var json = JsonSerializer.Serialize(response, options);

            logger.LogError("Returning error response to client. StatusCode: {StatusCode}, RequestId: {RequestId}",
                context.Response.StatusCode, context.TraceIdentifier);

            return context.Response.WriteAsync(json);
        }
    }
}
