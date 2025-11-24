using DropShot.Components;
using DropShot.Data;
using DropShot.Models;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();
builder.Services.AddDbContext<MyDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Register ACS settings for DI
builder.Services.Configure<AcsSettings>(builder.Configuration.GetSection("ACS"));
builder.Services.AddScoped<ISettingsService, SettingsService>();



builder.Services.AddHttpClient();
builder.Services.AddAuthorization();

builder.Services.AddHttpContextAccessor();

builder.Services.AddControllers();

builder.Services.AddServerSideBlazor()
    .AddCircuitOptions(options => options.DetailedErrors = true);

using (var tempProvider = builder.Services.BuildServiceProvider())
{
    var db = tempProvider.GetRequiredService<MyDbContext>();
    var settingsDict = db.AppSettings.ToDictionary(s => s.Setting, s => s.Value);

    var appConfig = new AppConfig
    {
        Theme = settingsDict["Theme"],
        PageSize = int.Parse(settingsDict["PageSize"]),
        BaseURL = settingsDict["BaseURL"]
    };

    // Register globally BEFORE Build()
    builder.Services.AddSingleton(appConfig);
}

builder.Services.AddHttpClient();

var app = builder.Build();

app.UseHttpsRedirection();

app.UseStaticFiles();
app.UseAntiforgery();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

//app.MapBlazorHub();
app.MapHub<ChatHub>("/chathub");
app.MapFallbackToFile("index.html");

app.Run();

public class AcsSettings
{
    public string ConnectionString { get; set; }
    public string SenderAddress { get; set; }
}


public class AppConfig
{
    public string Theme { get; set; }
    public int PageSize { get; set; }
    public string RegistrationEmailTo { get; set; }
    public string BaseURL { get; set; }
    // Add more settings as needed
}
