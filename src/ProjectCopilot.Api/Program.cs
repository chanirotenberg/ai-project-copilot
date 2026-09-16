using ProjectCopilot.Api.Middleware;

using ProjectCopilot.Application.Tasks.UpdateTask;

using ProjectCopilot.Api.Endpoints;

using FluentValidation;
using ProjectCopilot.Application.Projects.CreateProject;
using ProjectCopilot.Application.Tasks.CreateTask;

using ProjectCopilot.Application.Abstractions;
using ProjectCopilot.Infrastructure.Repositories;

using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ProjectCopilot.Infrastructure.Persistence;
using ProjectCopilot.Infrastructure.Identity;

using ProjectCopilot.Application.Auth.Register;
using ProjectCopilot.Application.Auth.Login;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("DefaultConnection")));

// This slice only issues JWTs (Register/Login). AddAuthentication/AddJwtBearer and
// UseAuthentication/UseAuthorization/[Authorize] are deliberately deferred to the next slice,
// since there is no protected endpoint yet to consume them.
builder.Services.AddIdentityCore<ApplicationUser>(options =>
    {
        options.Password.RequiredLength = 8;
        options.Password.RequireNonAlphanumeric = false;
        options.User.RequireUniqueEmail = true;
    })
    .AddRoles<IdentityRole<Guid>>()
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

builder.Services.AddScoped<IProjectRepository, ProjectRepository>();
builder.Services.AddScoped<ITaskRepository, TaskRepository>();

builder.Services.AddScoped<CreateProjectHandler>();
builder.Services.AddScoped<IValidator<CreateProjectCommand>, CreateProjectValidator>();

builder.Services.AddScoped<CreateTaskHandler>();
builder.Services.AddScoped<IValidator<CreateTaskCommand>, CreateTaskValidator>();

builder.Services.AddScoped<UpdateTaskHandler>();
builder.Services.AddScoped<IValidator<UpdateTaskCommand>, UpdateTaskValidator>();

builder.Services.AddScoped<IIdentityService, IdentityService>();
builder.Services.AddScoped<IJwtTokenGenerator, JwtTokenGenerator>();

builder.Services.AddScoped<RegisterHandler>();
builder.Services.AddScoped<IValidator<RegisterCommand>, RegisterValidator>();

builder.Services.AddScoped<LoginHandler>();
builder.Services.AddScoped<IValidator<LoginCommand>, LoginValidator>();

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