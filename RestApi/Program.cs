using Microsoft.EntityFrameworkCore;
using RestApi.Data;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// Rate Limiting
builder.Services.AddRateLimiter(options => // Rate limiting servisini ekliyoruz
{
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext => // Global rate limiter tanımı (tüm uygulamaya uygulanır)
        RateLimitPartition.GetFixedWindowLimiter( // Sabit zaman penceresi algoritması kullanılıyor
            partitionKey: httpContext.User.Identity?.Name ?? httpContext.Request.Headers.Host.ToString(), // Her kullanıcı veya host için ayrı limit uygula
            factory: partition => new FixedWindowRateLimiterOptions // Her bölüm için ayarları tanımla
            {
                AutoReplenishment = true, // Süre dolunca sayaç otomatik sıfırlansın
                PermitLimit = 4, // Her 10 saniyede en fazla 4 istek
                QueueLimit = 0, // Limit aşılırsa bekletme yok, istek reddedilir
                Window = TimeSpan.FromSeconds(10) // 10 saniyelik sabit pencere süresi
            }));
});

// Add services to the container.
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.AddControllers();
// Register PasswordManager to read configuration via DI
builder.Services.AddSingleton<RestApi.Services.PasswordManager>();


var  MyAllowSpecificOrigins = "_myAllowSpecificOrigins";
builder.Services.AddCors(options =>
{
    options.AddPolicy(name: MyAllowSpecificOrigins,
    policy  =>
    {
        policy.WithOrigins(
            "http://example.com",
            "http://www.contoso.com",
            "http://localhost:3000"
        );
    });
});



var key = Encoding.ASCII.GetBytes(builder.Configuration.GetValue<string>("Jwt:Key"));
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {

        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                if (context.Request.Cookies.ContainsKey("X-Access-Token"))
                {
                    context.Token = context.Request.Cookies["X-Access-Token"];
                }
                return Task.CompletedTask;
            }
        };

        options.RequireHttpsMetadata = false;
        options.SaveToken = true;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(key),
            ValidateIssuer = false,
            ValidateAudience = false
        };
    });
    

builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "X-CSRF-TOKEN";
    options.Cookie.Name = "X-CSRF-TOKEN";
    options.Cookie.HttpOnly = false; // JS erişebilsin diye
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.SuppressXFrameOptionsHeader = true;
});

var app = builder.Build();

// https config
if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
    app.UseHttpsRedirection();
    //app.UseXXSProtection( options => options.EnabledWithBlockMode());
}



// Güvenlik Headers
app.Use(async (context, next) =>
{

    context.Response.OnStarting(async () =>
    {
        context.Response.Headers.Remove("Server");
        context.Response.Headers.Remove("X-Powered-By");
        context.Response.Headers.Remove("X-AspNet-Version");
        context.Response.Headers.Remove("X-AspNetMvc-Version");
        await Task.CompletedTask;
    });

    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["X-Frame-Options"] = "DENY";
    context.Response.Headers["X-XSS-Protection"] = "1; mode=block";
    if (!context.Request.IsHttps)
    {
        //context.Response.Redirect("https://" + context.Request.Host + context.Request.Path + context.Request.QueryString, permanent: true);
        //return;
    }
    context.Response.Headers["Strict-Transport-Security"] = "max-age=31536000; includeSubDomains; preload";
    context.Response.Headers["Content-Security-Policy"] =
        "default-src 'self'; " +
        "script-src 'self'; " +
        "object-src 'none'; " +
        "frame-ancestors 'none'; " +
        "img-src 'self' data:; " +
        "media-src 'none'; " +
        "connect-src 'self'; " +
        "form-action 'self'; " +
        "base-uri 'self'; " +
        "upgrade-insecure-requests; " +
        "block-all-mixed-content; " +
        "camera 'none'; microphone 'none';";
    await next();
});

// Configure Cors policy
app.UseCors(MyAllowSpecificOrigins);

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}


app.UseMiddleware<GlobalExceptionHandler>();
app.UseMiddleware<GlobalMiddleware>();
app.UseRateLimiter();
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
