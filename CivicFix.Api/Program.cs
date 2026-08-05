using CivicFix.Api.Data;
using Microsoft.EntityFrameworkCore;
var builder = WebApplication.CreateBuilder(args);//builder is the assembler

// Add services to the container.

builder.Services.AddControllers();

builder.Services.AddOpenApi();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

//"My application will have controllers, use SQL Server, and support OpenAPI."
var app = builder.Build();//runnable object


if (app.Environment.IsDevelopment())//here the application well start runing
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();// Configure the HTTP request pipeline.

app.UseAuthorization();

app.MapControllers();

app.Run();
// this page is like setting.py and manage.py{ here where app is configured and lanched}