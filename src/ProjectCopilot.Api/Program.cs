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

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddScoped<IProjectRepository, ProjectRepository>();
builder.Services.AddScoped<ITaskRepository, TaskRepository>();

builder.Services.AddScoped<CreateProjectHandler>();
builder.Services.AddScoped<IValidator<CreateProjectCommand>, CreateProjectValidator>();

builder.Services.AddScoped<CreateTaskHandler>();
builder.Services.AddScoped<IValidator<CreateTaskCommand>, CreateTaskValidator>();

builder.Services.AddScoped<UpdateTaskHandler>();
builder.Services.AddScoped<IValidator<UpdateTaskCommand>, UpdateTaskValidator>();

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