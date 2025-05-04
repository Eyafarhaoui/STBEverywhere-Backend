using Chatbot.Repositories;
using Chatbot.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();

// Configuration nécessaire pour ChatbotService
builder.Services.AddSingleton<IWebHostEnvironment>(builder.Environment);
builder.Services.AddLogging(loggingBuilder =>
{
    loggingBuilder.AddConsole();
    loggingBuilder.AddDebug();
});

// Enregistrez le service local de synonymes
builder.Services.AddSingleton<LocalSynonymService>();

builder.Services.AddHttpClient<ChatbotService>();
builder.Services.AddSingleton<IIntentRepository, JsonIntentRepository>();
builder.Services.AddSingleton<ChatbotService>();
builder.Services.AddMemoryCache();

// Configuration Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "Chatbot API", Version = "v1" });
});

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAngular",
        builder => builder
            .WithOrigins("http://localhost:4200") // Port par défaut d'Angular
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials());
});
var app = builder.Build();
// Après builder.Build()
app.UseCors("AllowAngular");
// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Chatbot API v1");
    });
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

// Vérification de l'initialisation du ChatbotService au démarrage
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var chatbotService = services.GetRequiredService<ChatbotService>();
        app.Logger.LogInformation("ChatbotService initialisé avec succès");
    }
    catch (Exception ex)
    {
        app.Logger.LogError(ex, "Erreur lors de l'initialisation du ChatbotService");
    }
}




app.Run();