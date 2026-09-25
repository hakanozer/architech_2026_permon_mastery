using Microsoft.EntityFrameworkCore;
using RestApi.Data;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.Threading.RateLimiting;
using System.Diagnostics.Metrics;
using System.Diagnostics;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;

var builder = WebApplication.CreateBuilder(args);
var applicationMeter = new Meter("RestApi");
var requestCounter = applicationMeter.CreateCounter<long>("restapi_http_request_count", "{request}");
var requestDuration = applicationMeter.CreateHistogram<double>("restapi_http_request_duration", "s");
var activeRequests = applicationMeter.CreateUpDownCounter<long>("restapi_http_active_requests", "{request}");

var serviceName = builder.Configuration["OpenTelemetry:ServiceName"] ?? "rest-api";
var otlpEndpoint = new Uri(builder.Configuration["OpenTelemetry:OtlpEndpoint"] ?? "http://otel-collector:4317");

builder.Host.UseSerilog((context, _, loggerConfiguration) => loggerConfiguration
    .ReadFrom.Configuration(context.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.Seq(context.Configuration["Seq:Url"] ?? "http://seq:5341"));

builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService(serviceName))
    .WithMetrics(metrics => metrics
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddRuntimeInstrumentation()
        .AddProcessInstrumentation()
        .AddMeter(
            "RestApi",
            "Microsoft.AspNetCore.Hosting",
            "Microsoft.AspNetCore.Server.Kestrel",
            "System.Net.Http",
            "System.Net.NameResolution",
            "System.Net.Security",
            "System.Net.Sockets",
            "Microsoft.EntityFrameworkCore")
        .AddPrometheusExporter()
        .AddOtlpExporter(options =>
        {
            options.Endpoint = otlpEndpoint;
            options.BatchExportProcessorOptions.ScheduledDelayMilliseconds = 1000;
        }))
    .WithTracing(traces => traces
        .AddAspNetCoreInstrumentation(options =>
        {
            options.RecordException = true;
        })
        .AddHttpClientInstrumentation()
        .AddSqlClientInstrumentation()
        .AddOtlpExporter(options =>
        {
            options.Endpoint = otlpEndpoint;
        }));

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
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.AddHealthChecks().AddDbContextCheck<ApplicationDbContext>();
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


var jwtKeyValue = builder.Configuration.GetValue<string>("Jwt:Key");
if (string.IsNullOrEmpty(jwtKeyValue))
    throw new InvalidOperationException("Jwt:Key configuration is missing");

var key = Encoding.ASCII.GetBytes(jwtKeyValue);
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

app.Use(async (context, next) =>
{
    var stopwatch = System.Diagnostics.Stopwatch.StartNew();
    var tags = new TagList
    {
        { "http_request_method", context.Request.Method },
        { "http_route", context.Request.Path.Value ?? "/" }
    };
    activeRequests.Add(1, tags);
    try
    {
        await next();
    }
    finally
    {
        stopwatch.Stop();
        tags.Add("http_response_status_code", context.Response.StatusCode);
        requestCounter.Add(1, tags);
        requestDuration.Record(stopwatch.Elapsed.TotalSeconds, tags);
        activeRequests.Add(-1, tags);
    }
});

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    db.Database.Migrate();
}

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

app.MapHealthChecks("/health");
app.MapPrometheusScrapingEndpoint();
app.MapControllers();

app.Run();
