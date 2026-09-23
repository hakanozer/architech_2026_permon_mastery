using System.IdentityModel.Tokens.Jwt;
using System.Text;
using System.Text.Json;

public class GlobalMiddleware
{

    private readonly RequestDelegate _next;
    public GlobalMiddleware(RequestDelegate next)
    {
        _next = next;
    }
    public async Task Invoke(HttpContext context)
    {
        var url = context.Request.Path.Value?.ToLower();
        var ip = context.Connection.RemoteIpAddress?.ToString();
        var headers = context.Request.Headers;
        //var jwt = headers.Authorization.FirstOrDefault()?.Split(" ").Last();
        // jwt to username
        //var handler = new JwtSecurityTokenHandler();
        //var token = handler.ReadJwtToken(jwt);
        var username = context.User?.Identity?.Name;
        //var username = token?.Claims.FirstOrDefault(c => c.Type == "unique_name")?.Value;
        // request device info
        var deviceInfo = headers["User-Agent"].FirstOrDefault();

        var time = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        Console.WriteLine($"username: {username}, ip {ip}, url: {url}, time: {time}, device: {deviceInfo}");
        foreach (var header in headers)
        {
            //Console.WriteLine($"{header.Key}: {header.Value}");
        }
        
        await _next(context);
        
        
    }

}
