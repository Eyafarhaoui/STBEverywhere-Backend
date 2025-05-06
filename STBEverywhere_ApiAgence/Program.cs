using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using STBEverywhere_ApiAgence.Models;
using STBEverywhere_ApiAgence.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

// MongoDB Configuration with retry and timeout settings
var connectionString = builder.Configuration["MongoDBSettings:ConnectionString"];
var settings = MongoClientSettings.FromUrl(new MongoUrl(connectionString));
settings.ServerApi = new ServerApi(ServerApiVersion.V1);
settings.ConnectTimeout = TimeSpan.FromSeconds(30);
settings.SocketTimeout = TimeSpan.FromSeconds(30);
settings.ServerSelectionTimeout = TimeSpan.FromSeconds(30);
settings.MaxConnectionPoolSize = 100;
settings.MinConnectionPoolSize = 10;

var client = new MongoClient(settings);

// Enhanced connection test
try
{
    Console.WriteLine("Attempting MongoDB connection...");
    var db = client.GetDatabase("STBAgences");
    await db.RunCommandAsync((Command<BsonDocument>)"{ping:1}");
    Console.WriteLine("MongoDB connection successful!");

    // Verify collection exists
    var collections = await db.ListCollectionNames().ToListAsync();
    Console.WriteLine($"Collections: {string.Join(", ", collections)}");
}
catch (Exception ex)
{
    Console.WriteLine($"Connection error: {ex.Message}");
    // Consider terminating if DB connection is critical
    throw;
}

// Register MongoDB settings
builder.Services.Configure<MongoDBSettings>(
    builder.Configuration.GetSection("MongoDBSettings"));

// Register MongoClient as singleton
builder.Services.AddSingleton<IMongoClient>(serviceProvider =>
{
    var settings = serviceProvider.GetRequiredService<IOptions<MongoDBSettings>>().Value;
    var clientSettings = MongoClientSettings.FromUrl(new MongoUrl(settings.ConnectionString));
    clientSettings.ServerApi = new ServerApi(ServerApiVersion.V1);
    return new MongoClient(clientSettings);
});

// Register database
builder.Services.AddScoped<IMongoDatabase>(serviceProvider =>
{
    var client = serviceProvider.GetRequiredService<IMongoClient>();
    var settings = serviceProvider.GetRequiredService<IOptions<MongoDBSettings>>().Value;
    return client.GetDatabase(settings.DatabaseName);
});

// Register collection
builder.Services.AddScoped<IMongoCollection<Agence>>(serviceProvider =>
{
    var database = serviceProvider.GetRequiredService<IMongoDatabase>();
    var settings = serviceProvider.GetRequiredService<IOptions<MongoDBSettings>>().Value;
    return database.GetCollection<Agence>(settings.CollectionName);
});

// Register services
builder.Services.AddScoped<AgenceService>();

// Add controllers and Swagger
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAngularDev",
        builder => builder
            .WithOrigins("http://localhost:4200") // Autorisez votre URL Angular
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials());
});




var app = builder.Build();
app.UseCors("AllowAngularDev");
// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Initialize data
try
{

    using var scope = app.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<AgenceService>()
        .InitializeDataAsync("Data/listeAgencesStb.txt");
}
catch (Exception ex)
{
    Console.WriteLine($"Initialization error: {ex.Message}");
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();