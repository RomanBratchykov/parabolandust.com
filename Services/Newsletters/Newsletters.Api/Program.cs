using Microsoft.EntityFrameworkCore;
using Newsletters.Api.ExceptionHandling;
using Newsletters.Bll;
using Newsletters.Dal.Data;
using Newsletters.Dal.Seeding;
using Platform.ServiceDefaults;
using Serilog;
using Serilog.Context;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddNewslettersBll(builder.Configuration);
builder.Services.AddControllers();
builder.Services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
    context.ProblemDetails.Extensions["traceId"] = context.HttpContext.TraceIdentifier);
builder.Services.AddExceptionHandler<NewsletterExceptionHandler>();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.UseMiddleware<CorrelationIdMiddleware>();
app.Use(async (context, next) =>
{
    var userId = context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "anonymous";
    using (LogContext.PushProperty("UserId", userId))
    using (LogContext.PushProperty("RequestPath", context.Request.Path))
    {
        await next();
    }
});
app.UseSerilogRequestLogging();
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapControllers();
app.MapDefaultEndpoints();

await using (var scope = app.Services.CreateAsyncScope())
{
    var context = scope.ServiceProvider.GetRequiredService<NewslettersDbContext>();
    await context.Database.MigrateAsync();
    if (!app.Environment.IsProduction())
        await DbInitializer.SeedAsync(context);
}

app.Run();
