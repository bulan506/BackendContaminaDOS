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


builder.Services.Configure<MongoDbSettings>(options =>
{
    options.ConnectionString = Env.GetString("MONGODB_CONNECTION_STRING"); // Cargar desde el .env
    options.DatabaseName = "ContaminaDOS";  // Otras configuraciones fijas
    options.GamesCollectionName = "Games";
    options.RoundsCollectionName = "Rounds";
});

builder.Services.AddSingleton<MongoDbSettings>(sp =>
    sp.GetRequiredService<IOptions<MongoDbSettings>>().Value);

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

app.MapControllers();

app.Run();