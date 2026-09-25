
public class GlobalExceptionHandler
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(RequestDelegate next, ILogger<GlobalExceptionHandler> logger)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
        _logger = logger;
    }

    public async Task Invoke(HttpContext httpContext)
    {
        try
        {
            await _next(httpContext);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(httpContext, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext httpContext, Exception exception)
    {
        // opentelemetry jegager ui da görülmek üzere trace id oluşturuluyor
        var traceId = httpContext.TraceIdentifier;
        var errorId = Guid.NewGuid().ToString();

        // Detaylı log - sunucu tarafında
        _logger?.LogError(
            exception,
            "Unhandled exception: {Message} | Path: {Path} | User: {User} | IP: {IP} | ErrorId: {ErrorId}",
            exception.Message,
            httpContext.Request.Path,
            httpContext.User?.FindFirst("sub")?.Value,
            httpContext.Connection.RemoteIpAddress?.ToString(),
            errorId
        );


        // Kullanıcıya asla detay verme
        var response = new
        {
            error = "An unexpected error occurred. Please try again later.",
            code = errorId,
            timestamp = DateTime.UtcNow,
            traceId = traceId
        };

        httpContext.Response.Clear();
        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
        httpContext.Response.ContentType = "application/json";

        await httpContext.Response.WriteAsJsonAsync(response);
    }
}