using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using STBEverywhere_Back_SharedModels.Data;
using Microsoft.EntityFrameworkCore;
using System.Text;
using STBEverywhere_ApiAuth.Repositories;
using STBEverywheres_Back_ApiAuth.Services;
using Microsoft.OpenApi.Models;
using Microsoft.AspNetCore.Authorization;
using STBEverywhere_back_APIClient.Services;
using System.Security.Claims;

var builder = WebApplication.CreateBuilder(args);

// Configuration de la base de données
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseMySql(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        ServerVersion.Parse("8.0.30-mysql"),
        mySqlOptions => mySqlOptions.EnableRetryOnFailure()
    ));

// Configuration JWT
var jwtConfig = builder.Configuration.GetSection("Jwt");
var key = Encoding.UTF8.GetBytes(jwtConfig["Key"]);
var refreshKey = Encoding.UTF8.GetBytes(jwtConfig["RefreshKey"]);

// Configuration de l'authentification
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer("AccessToken", options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(key),
        ValidateIssuer = true,
        ValidIssuer = jwtConfig["Issuer"],
        ValidateAudience = true,
        ValidAudience = jwtConfig["Audience"],
        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero
    };
})
.AddJwtBearer("RefreshToken", options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(refreshKey),
        ValidateIssuer = true,
        ValidIssuer = jwtConfig["Issuer"],
        ValidateAudience = true,
        ValidAudience = jwtConfig["Audience"],
        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero,
        NameClaimType = "clientid", // Doit correspondre à l'API Client
        RoleClaimType = ClaimTypes.Role
    };
});

// Configuration de l'autorisation
builder.Services.AddAuthorization(options =>
{
    options.DefaultPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

// Services
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<EmailService>();
builder.Services.AddMemoryCache();
// CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.WithOrigins("http://localhost:4200") // Spécifiez vos origines
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials(); // Important pour les cookies
    });
});

// Swagger
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "API Auth",
        Version = "v1",
        Description = "API d'authentification STBEverywhere"
    });

    // Configuration pour JWT dans Swagger
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Example: \"Bearer {token}\"",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer",
        BearerFormat = "JWT"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

var app = builder.Build();

// Middleware pipeline - L'ORDRE EST CRUCIAL
app.UseRouting();

// CORS doit venir après UseRouting et avant UseAuthentication
app.UseCors("AllowAll");

// Authentification et Autorisation
app.UseAuthentication();
app.UseAuthorization();

// Swagger UI
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "API Auth v1");
        c.OAuthClientId("swagger-ui");
        c.OAuthAppName("Swagger UI");
        c.OAuthUsePkce();
    });
}

app.MapControllers();

app.Run();