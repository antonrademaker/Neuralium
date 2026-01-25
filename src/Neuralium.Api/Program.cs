// Neuralium.Api — HTTP API for feedback and queries
// Purpose: Provides REST endpoints for user feedback and news/trend queries.
// - POST /api/feedback/newsitem/{id} — Submit thumbs up/down on news items
// - POST /api/feedback/keyword/{id} — Submit thumbs up/down on keywords
// - Future GET endpoints for news queries and trends
//
// Uses controllers for complex feedback logic with validation.
// Future: Add authentication, rate limiting, caching.

using Microsoft.EntityFrameworkCore;
using Neuralium.Data;

var builder = WebApplication.CreateBuilder(args);

// Add database context
builder.AddNpgsqlDbContext<NeuraliumDbContext>("neuralium");

// Add controllers for feedback endpoints
builder.Services.AddControllers();

// Add API documentation (Swagger)
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddHealthChecks();

var app = builder.Build();

// Enable Swagger in development
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapControllers();
app.MapHealthChecks("/health");

// Available endpoints
var endpoints = new[]
{
    "POST /api/feedback/newsitem/{id} - Submit news item feedback",
    "POST /api/feedback/keyword/{id} - Submit keyword feedback",
    "GET /swagger - API documentation"
};

// Placeholder: Confirm API is running
app.MapGet("/", () => new
{
    Service = "Neuralium.Api",
    Status = "Running",
    Version = "1.0.0",
    Endpoints = endpoints
});

app.Run();
