using Duende.IdentityServer.EntityFramework.DbContexts;
using IdentityServerHost.Data;
using IdentityServerHost.Models;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Platform.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();
var connection = builder.Configuration.GetConnectionString("IdentityServerDb")
    ?? throw new InvalidOperationException("ConnectionStrings:IdentityServerDb is required.");
var keyPath = builder.Configuration["DataProtection:KeyPath"];
if (string.IsNullOrWhiteSpace(keyPath) || !Path.IsPathFullyQualified(keyPath))
    throw new InvalidOperationException("DataProtection:KeyPath must be an absolute persistent directory.");
var dataProtection = builder.Services.AddDataProtection().SetApplicationName("parabolandust-identityserver")
    .PersistKeysToFileSystem(new DirectoryInfo(keyPath));
if (OperatingSystem.IsWindows()) dataProtection.ProtectKeysWithDpapi();

var assembly = typeof(Program).Assembly.GetName().Name;
builder.Services.AddDbContext<ApplicationDbContext>(options => options.UseNpgsql(connection,
    postgres => postgres.MigrationsHistoryTable("__IdentityMigrationsHistory")));
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.Password.RequiredLength = 8;
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
    options.SignIn.RequireConfirmedEmail = true;
    options.User.RequireUniqueEmail = true;
}).AddEntityFrameworkStores<ApplicationDbContext>().AddDefaultTokenProviders();
builder.Services.AddIdentityServer(options =>
    {
        options.LicenseKey = builder.Configuration["Duende:LicenseKey"];
    })
    .AddAspNetIdentity<ApplicationUser>()
    .AddConfigurationStore(options => options.ConfigureDbContext = db => db.UseNpgsql(connection,
        postgres => postgres.MigrationsAssembly(assembly).MigrationsHistoryTable("__ConfigurationMigrationsHistory")))
    .AddOperationalStore(options =>
    {
        options.ConfigureDbContext = db => db.UseNpgsql(connection,
            postgres => postgres.MigrationsAssembly(assembly).MigrationsHistoryTable("__OperationalMigrationsHistory"));
        options.EnableTokenCleanup = true;
    });
builder.Services.AddRazorPages();

var app = builder.Build();
await using (var scope = app.Services.CreateAsyncScope())
{
    await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Database.MigrateAsync();
    await scope.ServiceProvider.GetRequiredService<ConfigurationDbContext>().Database.MigrateAsync();
    await scope.ServiceProvider.GetRequiredService<PersistedGrantDbContext>().Database.MigrateAsync();
    await ConfigurationSeeder.SeedAsync(scope.ServiceProvider, builder.Configuration, builder.Environment);
}
app.UseStaticFiles();
app.UseRouting();
app.UseIdentityServer();
app.UseAuthorization();
app.MapRazorPages();
app.MapDefaultEndpoints();
app.Run();
