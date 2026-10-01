using Microsoft.EntityFrameworkCore;
using ProductService.API.Data;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

builder.Services.AddDbContext<ProductDbContext>(options =>
{
    options.UseSqlite(
        builder.Configuration.GetConnectionString("DefaultConnection"));
});

builder.Services.AddHealthChecks();

builder.Services.AddOutputCache(options =>
{
    options.AddPolicy("ProductsCache",
        policy =>
        {
            policy.Expire(TimeSpan.FromSeconds(30));
        });
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();
app.UseOutputCache();

app.MapHealthChecks("/health");

app.MapControllers();

app.Run();