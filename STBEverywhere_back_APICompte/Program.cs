
using Microsoft.EntityFrameworkCore;
using STBEverywhere_back_APICompte.Repository.IRepository;
using STBEverywhere_back_APICompte.Repository;
using STBEverywhere_back_APICompte;
using STBEverywhere_Back_SharedModels.Data;
using Microsoft.OpenApi.Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using STBEverywhere_back_APICompte.Services;
using STBEverywhere_back_APICompte.Filters;
using STBEverywhere_ApiAuth.Repositories;
using Newtonsoft.Json;
using STBEverywhere_back_APIClient.Repositories;
using STBEverywhere_back_APICompte.Jobs;
using DinkToPdf.Contracts;
using DinkToPdf;

using QuestPDF.Infrastructure;

using System.Runtime.InteropServices;
using System.Net.Http.Headers;
using STBEverywhere_back_APICompte.Services.IServices;
using Hangfire;
using Hangfire.MySql;


var builder = WebApplication.CreateBuilder(args);
builder.Services.AddDbContext<ApplicationDbContext>(options =>
  options.UseMySql(
      builder.Configuration.GetConnectionString("DefaultConnection"),
      ServerVersion.Parse("8.0.0-mysql")
  ));

// Add services to the container.

builder.Services.AddHangfire(configuration =>
{
    configuration.UseStorage(new MySqlStorage(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        new MySqlStorageOptions
        {
            TablesPrefix = "Hangfire",
            QueuePollInterval = TimeSpan.FromSeconds(10)
        }
    ));
});



// Démarre le serveur Hangfire
builder.Services.AddHangfireServer();


builder.Services.AddScoped<IHistoriqueSoldeService, HistoriqueSoldeService>(); 
builder.Services.AddScoped<ICompteRepository, CompteRepository>();
builder.Services.AddScoped<IVirementRepository, VirementRepository>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<ICompteService, CompteService>();
builder.Services.AddScoped<IOtpService, OtpService>();

builder.Services.AddScoped<IVirementRepository, VirementRepository>();

builder.Services.AddScoped<IBeneficiaireRepository, BeneficiaireRepository>();
builder.Services.AddScoped<IFraisCompteRepository, FraisCompteRepository>();
//builder.Services.AddScoped<IVirementService, VirementService>();
builder.Services.AddHostedService<DemandeModificationDecouvertJob>();
builder.Services.AddScoped<IEmailLogRepository, EmailLogRepository>();
builder.Services.AddScoped<EmailService>();
builder.Services.AddScoped<CalculInteretsService>();

builder.Services.AddSingleton(typeof(IConverter), new SynchronizedConverter(new PdfTools()));
builder.Services.AddAutoMapper(typeof(MappingConfig));
builder.Services.AddControllers().AddNewtonsoftJson();

builder.Services.AddScoped<DecouvertTrackerService>();
builder.Services.AddScoped<AgiosService>();
//builder.Services.AddHostedService<AgiosBackgroundService>();
string agenceServiceUrl = "http://localhost:5036"; // URL définie en dur

builder.Services.AddHttpClient("AgenceService", client =>
{
    client.BaseAddress = new Uri(agenceServiceUrl);
    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
});


// pour generer le pdf 


// Ajoutez ceci avant builder.Build()
/*var c = new CustomAssemblyLoadContext();
var dllPath = Path.Combine(Directory.GetCurrentDirectory(), "libwkhtmltox.dll");
c.LoadUnmanagedLibrary(dllPath);

builder.Services.AddSingleton(typeof(IConverter), new SynchronizedConverter(new PdfTools()));*/


// Dans Program.cs
/*Console.WriteLine($"OS Architecture: {RuntimeInformation.OSArchitecture}");
Console.WriteLine($"Process Architecture: {RuntimeInformation.ProcessArchitecture}");*/


// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
//builder.Services.AddSwaggerGen();

builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description =
        "JWT Authorization header using the Bearer scheme. \r\n\r\n " +
        "Enter 'Bearer' [space] and then your token in the text input below. \r\n\r\n" +
        "Example: \"Bearer 12345abcdef\"",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Scheme = "Bearer"
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                },
                Scheme = "Bearer",
                Name = "Authorization",
                In = ParameterLocation.Header
            },
            new List<string>()
        }
    });

    // Ajoute le filtre pour gérer les fichiers dans Swagger
   options.OperationFilter<SwaggerFileOperationFilter>();

    options.SchemaGeneratorOptions.SupportNonNullableReferenceTypes = false;

});

var AllowedOrigins = builder.Configuration.GetValue<string>("AllowedOrigins")!.Split(",");
builder.Services.AddCors(Options =>
{
    Options.AddDefaultPolicy(policy => {
        policy.WithOrigins(AllowedOrigins).AllowAnyHeader().AllowAnyMethod();
    });
});


// Configuration de l'authentification JWT
var key = Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]);

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.RequireHttpsMetadata = false; // À activer en production
        options.SaveToken = true;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(key),
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };
    });






//ignorer le json relation circulaire 
builder.Services.AddControllers()
    .AddNewtonsoftJson(options =>
    {
        options.SerializerSettings.ReferenceLoopHandling = ReferenceLoopHandling.Ignore;
    });


builder.Services.AddAuthorization();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAngularOrigins", policy =>
    {
        policy.WithOrigins("http://localhost:4200")
              .AllowAnyHeader()
              .AllowCredentials()
              .AllowAnyMethod();
    });
});
QuestPDF.Settings.License = LicenseType.Community;
var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var agiosService = scope.ServiceProvider.GetRequiredService<AgiosService>();
    await agiosService.CalculerEtAppliquerAgiosMensuels();
}

using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var context = services.GetRequiredService<ApplicationDbContext>();
    //context.Database.Migrate();
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
//app.UseStaticFiles();
app.UseHttpsRedirection();

app.UseRouting(); // 

app.UseAuthentication();
app.UseCors("AllowAngularOrigins");
app.UseAuthorization();

app.UseHangfireDashboard("/hangfire");


// Job quotidien à minuit
RecurringJob.AddOrUpdate<CalculInteretsService>(
    x => x.CalculerInteretsQuotidiens(),
    Cron.Daily
);

// Job trimestriel (1er jour de chaque trimestre)
RecurringJob.AddOrUpdate<CalculInteretsService>(
    x => x.VerserInteretsTrimestriels(),
    "0 0 1 1,4,7,10 *" // 1er Janvier, Avril, Juillet, Octobre à minuit
);



RecurringJob.AddOrUpdate<IHistoriqueSoldeService>(
    x => x.AlimenterHistoriqueSolde(),
    Cron.Daily,
    TimeZoneInfo.FindSystemTimeZoneById("Africa/Tunis")
);


//hangfire des agios
RecurringJob.AddOrUpdate<AgiosService>(
    x => x.CalculerEtAppliquerAgiosMensuels(),
    "0 3 1 * *", // Le 1er de chaque mois à 03h00 du matin
    TimeZoneInfo.FindSystemTimeZoneById("Africa/Tunis")
);

app.UseEndpoints(endpoints =>
{
    endpoints.MapControllers();
});

app.Run();