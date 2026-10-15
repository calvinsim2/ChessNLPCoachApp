using ChessNLPCoachApp.Configuration;
using ChessNLPCoachApp.Context;
using ChessNLPCoachApp.Interfaces;
using ChessNLPCoachApp.Services;
using DotNetEnv;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

string envPath = Path.Combine(
    AppContext.BaseDirectory,
    ".env");

// Load .env only if it exists.
// Azure will use environment variables instead.
if (File.Exists(envPath))
{
    Env.Load(envPath);
}

// Read the local connection string from configuration.
string connectionString =
    builder.Configuration.GetConnectionString("DefaultConnection") ?? 
    throw new InvalidOperationException("DefaultConnection connection string is missing.");

builder.Services.AddDbContext<ApplicationDbContext>(
    options => options.UseSqlServer(connectionString));

// ========================================
// ASP.NET CORE IDENTITY
// ========================================

// Register Identity's user and role management services.
builder.Services
    .AddIdentity<IdentityUser, IdentityRole>(options =>
    {
        options.Password.RequiredLength = 10;

        options.Lockout.DefaultLockoutTimeSpan =
            TimeSpan.FromMinutes(15);

        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.AllowedForNewUsers = true;
    })
    // Store user accounts using ApplicationDbContext.
    .AddEntityFrameworkStores<ApplicationDbContext>();

// ========================================
// AUTHENTICATION COOKIE
// ========================================

builder.Services.ConfigureApplicationCookie(options =>
{
    // Redirect unauthenticated visitors here.
    options.LoginPath = "/Account/Login";

    // The name of the authentication cookie.
    options.Cookie.Name = "ChessNLPCoach.Auth";

    // Prevent JavaScript from reading the cookie.
    options.Cookie.HttpOnly = true;

    // Only transmit the cookie over HTTPS.
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;

    // Configure the session lifetime.
    options.ExpireTimeSpan = TimeSpan.FromHours(2);

    // Do not extend the session automatically.
    options.SlidingExpiration = false;
});

// ==========================================
// DAILY ANALYSIS QUOTA
// ==========================================
builder.Services.AddScoped<DailyAnalysisQuotaService>();

// ==========================================
// CONCURRENT ANALYSIS PROTECTION
// ==========================================

// Only one analysis can run per application instance.
builder.Services.AddSingleton(
    new SemaphoreSlim(1, 1));

// ==========================================
// HOURLY UPLOAD-ATTEMPT LIMIT
// ==========================================
builder.Services.AddRateLimiter(options =>
{
    // Return 429 when the upload limit is exceeded.
    options.RejectionStatusCode =
        StatusCodes.Status429TooManyRequests;

    // Apply independent limits per authenticated user.
    options.AddPolicy("upload-attempts", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey:
                context.User.FindFirstValue(
                    ClaimTypes.NameIdentifier) ?? "anonymous",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit =
                    AnalysisLimits.HourlyUploadAttempts,

                Window = TimeSpan.FromHours(1),
                QueueLimit = 0,
                AutoReplenishment = true
            }));
});

// Add services to the container.
builder.Services.AddControllersWithViews();

builder.Services.AddScoped<IPgnService, PgnService>();

// ==========================================
// LLM COACHING CLIENTS
// ==========================================

// Local development: Langflow running on the developer's machine.
// Used for prompt testing without calling the hosted model.
// Configured via LANGFLOW_BASE_URL / LANGFLOW_FLOW_ID / LANGFLOW_API_KEY.
builder.Services.AddScoped<ILangflowClient>(sp =>
    new LangflowClient(Environment.GetEnvironmentVariable("LANGFLOW_BASE_URL"),
                       Environment.GetEnvironmentVariable("LANGFLOW_FLOW_ID"),
                       Environment.GetEnvironmentVariable("LANGFLOW_API_KEY")));

// Production: hosted model on Azure AI Foundry.
// Configured via Foundry:Endpoint / Foundry:Deployment / Foundry:ApiKey.
builder.Services.AddScoped<IFoundryChessCoachClient,FoundryChessCoachClient>();

builder.Services.AddScoped<IStockfishClient>(sp =>
    new StockfishClient(builder.Configuration["Stockfish:Path"]
        ?? throw new InvalidOperationException("Stockfish:Path is not configured in appsettings.json.")));
builder.Services.AddScoped<IMoveAnalysisService, MoveAnalysisService>();

var app = builder.Build();

// ========================================
// CREATE INITIAL LOCAL TEST USER
// ========================================

// Only run this account-provisioning step locally.
if (app.Environment.IsDevelopment())
{
    // Read temporary credentials from User Secrets.
    string? username = app.Configuration["SeedUser:Username"];
    string? password = app.Configuration["SeedUser:Password"];

    // Continue only if both values are configured.
    if (!string.IsNullOrWhiteSpace(username) &&
        !string.IsNullOrWhiteSpace(password))
    {
        // Create a dependency injection scope.
        using var scope = app.Services.CreateScope();

        // Retrieve Identity's user management service.
        UserManager<IdentityUser> userManager = scope.ServiceProvider
            .GetRequiredService<UserManager<IdentityUser>>();

        // Check whether the account already exists.
        IdentityUser? existingUser = await userManager.FindByNameAsync(username);

        if (existingUser == null)
        {
            // Construct the new user account.
            IdentityUser user = new IdentityUser
            {
                UserName = username
            };

            // Identity automatically hashes the password.
            IdentityResult? result = await userManager.CreateAsync(user, password);

            // Check whether account creation succeeded.
            if (!result.Succeeded)
            {
                throw new InvalidOperationException(
                    "Failed to create local test user: " +
                    string.Join("; ",
                        result.Errors.Select(e => e.Description)));
            }
            else
            {
                Console.WriteLine("Created Local test user!");
            }
        }
        else
        {
            Console.WriteLine("Local test user already exists, no Local test user created");
        }
    }
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

app.UseAuthentication();
app.UseRateLimiter();
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Account}/{action=Login}/{id?}")
    .WithStaticAssets();


app.Run();
