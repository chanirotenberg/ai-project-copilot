using ProjectCopilot.Api.Middleware;

using ProjectCopilot.Application.Tasks.UpdateTask;

using ProjectCopilot.Api.Endpoints;

using FluentValidation;
using ProjectCopilot.Application.Projects.CreateProject;
using ProjectCopilot.Application.Tasks.CreateTask;

using ProjectCopilot.Application.Abstractions;
using ProjectCopilot.Infrastructure.Repositories;

using Microsoft.EntityFrameworkCore;
using ProjectCopilot.Infrastructure.Persistence;
using ProjectCopilot.Infrastructure.Identity;

using ProjectCopilot.Application.Auth.Register;
using ProjectCopilot.Application.Auth.Login;
using ProjectCopilot.Application.Auth.Refresh;

using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// Pins all FluentValidation built-in messages to English regardless of host/container
// locale, fixing a Hebrew/English mix found in QA.
FluentValidation.ValidatorOptions.Global.LanguageManager.Enabled = false;

// Development-only CORS policy so the Vite dev server (different origin)
// can call the API. Explicit origin, no wildcard, no credentials (the
// frontend sends tokens in the JSON body, never cookies). Has zero effect
// outside Development: see the `UseCors` gate below.
const string DevCorsPolicy = "DevCors";

builder.Services.AddCors(options =>
{
    options.AddPolicy(DevCorsPolicy, policy =>
        policy
            .WithOrigins("http://localhost:5173")
            .WithMethods("GET", "POST", "PUT")
            .WithHeaders("Authorization", "Content-Type"));
});

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddIdentityCore<ApplicationUser>(options =>
    {
        // Relaxed password policy: favors passphrase length over composition-rule UX friction.
        // Only affects future registrations/password changes - existing password hashes are
        // never re-validated against this policy (login only compares hashes).
        options.Password.RequiredLength = 10;
        options.Password.RequireDigit = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireUppercase = false;
        options.Password.RequireNonAlphanumeric = false;
        options.User.RequireUniqueEmail = true;
    })
    .AddEntityFrameworkStores<AppDbContext>();

builder.Services.AddOptions<JwtOptions>()
    .Bind(builder.Configuration.GetSection("Jwt"))
    .Validate(
        o => !string.IsNullOrWhiteSpace(o.Key) && System.Text.Encoding.UTF8.GetByteCount(o.Key) >= 32,
        "Jwt:Key must be configured and at least 32 bytes (UTF-8).")
    .Validate(
        o => !string.IsNullOrWhiteSpace(o.Issuer) && !string.IsNullOrWhiteSpace(o.Audience),
        "Jwt:Issuer and Jwt:Audience must both be configured.")
    .ValidateOnStart();

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer();

builder.Services
    .AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<IOptions<JwtOptions>>((bearerOptions, jwtOptionsAccessor) =>
    {
        var jwt = jwtOptionsAccessor.Value;
        bearerOptions.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwt.Issuer,
            ValidAudience = jwt.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key))
        };
    });

builder.Services.AddAuthorization();

builder.Services.AddScoped<IProjectRepository, ProjectRepository>();
builder.Services.AddScoped<ITaskRepository, TaskRepository>();
builder.Services.AddScoped<IProjectMembershipService, ProjectMembershipService>();

builder.Services.AddScoped<CreateProjectHandler>();
builder.Services.AddScoped<IValidator<CreateProjectCommand>, CreateProjectValidator>();

builder.Services.AddScoped<CreateTaskHandler>();
builder.Services.AddScoped<IValidator<CreateTaskCommand>, CreateTaskValidator>();

builder.Services.AddScoped<UpdateTaskHandler>();
builder.Services.AddScoped<IValidator<UpdateTaskCommand>, UpdateTaskValidator>();

builder.Services.AddScoped<IIdentityService, IdentityService>();
builder.Services.AddScoped<IJwtTokenGenerator, JwtTokenGenerator>();
builder.Services.AddScoped<IRefreshTokenGenerator, RefreshTokenGenerator>();
builder.Services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();

builder.Services.AddScoped<RegisterHandler>();
builder.Services.AddScoped<IValidator<RegisterCommand>, RegisterValidator>();

builder.Services.AddScoped<LoginHandler>();
builder.Services.AddScoped<IValidator<LoginCommand>, LoginValidator>();

builder.Services.AddScoped<RefreshHandler>();
builder.Services.AddScoped<IValidator<RefreshCommand>, RefreshCommandValidator>();

builder.Services.AddScoped<DemoDataSeeder>();

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (args.Contains("--seed-demo"))
{
    using var scope = app.Services.CreateScope();

    var seeder = scope.ServiceProvider
        .GetRequiredService<DemoDataSeeder>();

    await seeder.SeedAsync();

    return;
}

app.UseMiddleware<ExceptionHandlingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseCors(DevCorsPolicy);
}

app.UseAuthentication();
app.UseAuthorization();

app.MapProjectsEndpoints();
app.MapTasksEndpoints();
app.MapAuthEndpoints();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

if (!app.Environment.IsEnvironment("Testing"))
{
    app.UseHttpsRedirection();
}

app.Run();

public partial class Program;