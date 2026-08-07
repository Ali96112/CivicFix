using CivicFix.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);//builder is the assembler

// Add services to the container.

builder.Services.AddControllers();

builder.Services.AddScoped<SqlConnection>(sp =>
    new SqlConnection(builder.Configuration.GetConnectionString("DefaultConnection")));//Registers a raw SQL connection so controllers can receive it and write plain SQL queries through Dapper.


builder.Services.AddDbContext<AppDbContext>(options =>
    // Register AppDbContext(db key) so controllers can receive _context
    options.UseSqlServer(
        // Use SQL Server as the database engine
        builder.Configuration.GetConnectionString("DefaultConnection"),
        // Read the database address from appsettings.json
        x => x.UseNetTopologySuite()
        // Enable spatial/map support (Point, Polygon, etc.)
    ));
//"My application will have controllers, use SQL Server"


builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,           // check token came from "CivicFix"
            ValidateAudience = true,          // check token is meant for "CivicFixUsers"
            ValidateLifetime = true,          // check token hasn't expired
            ValidateIssuerSigningKey = true,  // check token signature wasn't tampered with
            ValidIssuer = builder.Configuration["Jwt:Issuer"],        // reads "CivicFix" from appsettings.json
            ValidAudience = builder.Configuration["Jwt:Audience"],    // reads "CivicFixUsers" from appsettings.json
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"])) // reads secret key from appsettings.json
        };
    });

var app = builder.Build();//runnable object

app.UseHttpsRedirection();// Configure the HTTP request pipeline.

app.UseAuthentication(); // reads the token from the request, validates it, extracts user info

app.UseAuthorization();  // checks if that user is allowed to do this action

app.MapControllers();//program here map controller with correct route

app.Run();
// this page is like setting.py and manage.py{ here where app is configured and lanched}