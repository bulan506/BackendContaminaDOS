using Core.Models.Data;
using Microsoft.Extensions.Options;
using Core.Models.Business;
using DotNetEnv; // Asegúrate de importar esta librería

var builder = WebApplication.CreateBuilder(args);
Env.Load();

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Add CORS services
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll",
        builder => builder.AllowAnyOrigin()
                          .AllowAnyMethod()
                          .AllowAnyHeader());
});


builder.Services.Configure<DbSettings>(options =>
{
    try
    {
        options.ConnectionString = Env.GetString("DB_CONNECTION_STRING")
            ?? Environment.GetEnvironmentVariable("DB_CONNECTION_STRING")
            ?? throw new InvalidOperationException("Database connection is not configured.");

        options.DatabaseName = "ContaminaDOS";
        options.GamesCollectionName = "Games";
        options.RoundsCollectionName = "Rounds";
    }
    catch (Exception ex)
    {
        throw new Exception($"Error, db configuration: {ex.Message}");
    }
});


builder.Services.AddSingleton<DbSettings>(sp =>
    sp.GetRequiredService<IOptions<DbSettings>>().Value);

builder.Services.AddScoped<IGameCreationService, GameCreationService>();
builder.Services.AddScoped<IGameService, GameService>();



var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Use CORS
app.UseCors("AllowAll");

//app.UseHttpsRedirection();

//app.UseAuthorization();
app.UseExceptionHandler("/error");

// Map error controller
app.MapControllers();
app.Map("/error", (HttpContext context) =>
{
    context.Response.StatusCode = StatusCodes.Status500InternalServerError;
    return Results.Problem(
        title: "Internal Server Error",
        detail: "An unexpected error occurred. Please contact support if the issue persists.",
        statusCode: 500
    );
});

app.Run();