using Microsoft.EntityFrameworkCore;
using backend.Data;
using backend.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// SQLite
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite("Data Source=NegSituations.db"));

// CORS для Angular (http://localhost:4200)
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAngular",
        policy => policy.WithOrigins("http://localhost:4200")
                        .AllowAnyHeader()
                        .AllowAnyMethod());
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
// Создаём БД при запуске
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
    db.Database.ExecuteSqlRaw("PRAGMA foreign_keys = ON;");
    // Сидируем факторы, если их нет
if (!db.Factors.Any())
{
    db.Factors.AddRange(
        new Factor { Name = "Организационный" },
        new Factor { Name = "Технический" },
        new Factor { Name = "Психофизиологический" },
        new Factor { Name = "Внешний" }
    );

    db.SaveChanges();
}
}

app.UseCors("AllowAngular");
app.UseAuthorization();
app.MapControllers();

app.Run();