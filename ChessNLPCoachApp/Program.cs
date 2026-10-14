using ChessNLPCoachApp.Interfaces;
using ChessNLPCoachApp.Services;
using DotNetEnv;

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

// Add services to the container.
builder.Services.AddControllersWithViews();

builder.Services.AddScoped<IPgnService, PgnService>();
builder.Services.AddScoped<ILangflowClient>(sp =>
    new LangflowClient(Environment.GetEnvironmentVariable("LANGFLOW_BASE_URL"),
                       Environment.GetEnvironmentVariable("LANGFLOW_FLOW_ID"),
                       Environment.GetEnvironmentVariable("LANGFLOW_API_KEY")));
builder.Services.AddScoped<IStockfishClient>(sp =>
    new StockfishClient(builder.Configuration["Stockfish:Path"]
        ?? throw new InvalidOperationException("Stockfish:Path is not configured in appsettings.json.")));
builder.Services.AddScoped<IMoveAnalysisService, MoveAnalysisService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Analysis}/{action=Index}/{id?}")
    .WithStaticAssets();


app.Run();
