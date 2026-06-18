using GameServer.Business.Services;
using GameServer.Data;
using GameServer.Presentation.Endpoints;
using GameServer.Presentation.Hubs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials()
            .SetIsOriginAllowed(_ => true);
    });
});

builder.Services.AddSignalR();
builder.Services.AddScoped<GameStateService>();
builder.Services.AddDbContext<GameDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        sqlOptions => sqlOptions.EnableRetryOnFailure()));

var app = builder.Build();

if (app.Configuration.GetValue("Database:AutoMigrate", true))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<GameDbContext>();
    db.Database.Migrate();
}

var frontendPath = Path.GetFullPath(Path.Combine(app.Environment.ContentRootPath, "..", "Frontend"));
if (Directory.Exists(frontendPath))
{
    var frontendFiles = new PhysicalFileProvider(frontendPath);
    app.UseDefaultFiles(new DefaultFilesOptions { FileProvider = frontendFiles });
    app.UseStaticFiles(new StaticFileOptions { FileProvider = frontendFiles });
    app.MapFallbackToFile("index.html", new StaticFileOptions { FileProvider = frontendFiles });
}
else
{
    app.UseDefaultFiles();
    app.UseStaticFiles();
    app.MapFallbackToFile("index.html");
}
app.UseCors();

app.MapHub<GameHub>("/gameHub");
app.MapGameEndpoints();

app.Run();
