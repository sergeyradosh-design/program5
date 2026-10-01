using System.Net;
using System.Net.Http;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddTransient<INotificationService, EmailNotificationService>();
builder.Services.AddTransient<INotificationService, SmsNotificationService>();

var app = builder.Build();

app.UseMiddleware<ErrorHandlingMiddleware>();

app.MapGet("/notify", async (HttpContext context, IEnumerable<INotificationService> notificationServices, string? message) =>
{
    if (string.IsNullOrWhiteSpace(message))
    {
        throw new ArgumentException("Повідомлення не може бути порожнім!");
    }

    var results = new List<string>();
    foreach (var service in notificationServices)
    {
        results.Add(await service.SendAsync(message));
    }

    return Results.Ok(new
    {
        Status = "Success",
        SentNotifications = results
    });
});

app.MapGet("/error-test", () =>
{
    throw new InvalidOperationException("Тестова критична помилка сервера!");
});

app.Run();

public interface INotificationService
{
    Task<string> SendAsync(string message);
}

public class EmailNotificationService : INotificationService
{
    public Task<string> SendAsync(string message)
    {
        return Task.FromResult($"Email надіслано: {message}");
    }
}

public class SmsNotificationService : INotificationService
{
    public Task<string> SendAsync(string message)
    {
        return Task.FromResult($"SMS надіслано: {message}");
    }
}

public class ErrorHandlingMiddleware
{
    private readonly RequestDelegate _next;

    public ErrorHandlingMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private static Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        context.Response.ContentType = "application/json";

        var statusCode = exception switch
        {
            ArgumentException => (int)HttpStatusCode.BadRequest,
            _ => (int)HttpStatusCode.InternalServerError
        };

        context.Response.StatusCode = statusCode;

        var response = new
        {
            StatusCode = statusCode,
            Message = exception.Message,
            Type = exception.GetType().Name
        };

        return context.Response.WriteAsync(JsonSerializer.Serialize(response));
    }
}
