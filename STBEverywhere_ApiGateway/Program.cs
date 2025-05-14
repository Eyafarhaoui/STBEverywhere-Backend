using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Activer les logs détaillés
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

// Configuration CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAngular", builder =>
    {
        builder.WithOrigins("http://localhost:4200")
               .AllowAnyMethod()
               .AllowAnyHeader()
               .AllowCredentials();
    });
});

// Configuration de l'authentification JWT
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"])),
            NameClaimType = ClaimTypes.NameIdentifier,
            RoleClaimType = ClaimTypes.Role
        };
    });

builder.Services.AddAuthorization();

// Ajouter les services YARP
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

// Configuration Kestrel pour les requêtes volumineuses
builder.WebHost.ConfigureKestrel(serverOptions =>
{
    serverOptions.Limits.MaxRequestBodySize = 5242880; // 5MB
});

builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 5242880; // 5MB
    options.MultipartBoundaryLengthLimit = int.MaxValue;
    options.MemoryBufferThreshold = int.MaxValue;
});

var app = builder.Build();

// Middleware pour gérer les requêtes OPTIONS (CORS Preflight)
app.Use(async (context, next) =>
{
    if (context.Request.Method == "OPTIONS")
    {
        await next();
        return;
    }

    await next();
});

app.UseStaticFiles();
app.UseRouting();
app.UseCors("AllowAngular"); // Activation CORS
app.UseAuthentication();
app.UseAuthorization();

// Middleware d'authentification personnalisé
app.Use(async (context, next) =>
{
    var path = context.Request.Path;

    // Routes publiques
    if (path.StartsWithSegments("/api/auth/login") ||
        path.StartsWithSegments("/api/Client/register") ||
        path.StartsWithSegments("/api/client/upload-documents") ||
        path.StartsWithSegments("/api/compte/GetSoldeByRIB") ||
        path.StartsWithSegments("/api/AgenceApi") ||
         path.StartsWithSegments("/api/AgenceApi/byId") ||
        path.StartsWithSegments("/api/compte/GetByRIB"))
    {
        await next();
        return;
    }

    // Vérification des endpoints protégés
    var endpoint = context.GetEndpoint();
    if (endpoint?.Metadata.GetMetadata<AuthorizeAttribute>() != null)
    {
        if (!context.User.Identity.IsAuthenticated)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsync("Unauthorized");
            return;
        }
    }

    await next();
});

// Middleware de vérification des rôles Agent
app.Use(async (context, next) =>
{
    var path = context.Request.Path;

    if (path.StartsWithSegments("/api/agent"))
    {
        if (!context.User.Identity?.IsAuthenticated ?? true)
        {
            context.Response.StatusCode = 401;
            await context.Response.WriteAsync("Token manquant ou invalide");
            return;
        }

        if (!context.User.IsInRole("Agent"))
        {
            context.Response.StatusCode = 403;
            await context.Response.WriteAsync("Accès réservé aux agents");
            return;
        }
    }

    await next();
});

app.MapReverseProxy();
app.Run("http://localhost:5000");