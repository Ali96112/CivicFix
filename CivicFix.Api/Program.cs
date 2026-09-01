using CivicFix.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
//MSSQLLocalDB

//this program first thing that run
var builder = WebApplication.CreateBuilder(args);//builder is the assembler

// Add services to the container.

builder.Services.AddControllers()// when a request arrive it take it to controller folder

    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = null;
    });

builder.Services.AddScoped<SqlConnection>(sp =>
    new SqlConnection(builder.Configuration.GetConnectionString("DefaultConnection")));//AddScoped means a new connection is created per request and closed when the request finishes
///this is what injected in _connection:database setting in appsetting

builder.Services.AddDbContext<AppDbContext>(options =>  //Registering AppDbContext but EF core read it at migration time
    // appdbcontesxt used for migration to update/insert
    options.UseSqlServer(
        //tell EF to Use SQL Server as the database engine
        builder.Configuration.GetConnectionString("DefaultConnection"),
        //tell EF to Read the database address from appsettings.json
        x => x.UseNetTopologySuite()
        // this line activate the bridge  between the EF and and the installed NETTEPOLYGY library
    ));
//"My application will have controllers, use SQL Server"


builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters//sets validation rule fore every token want to pass
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


builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowReact", policy =>
    {
        policy.WithOrigins("http://localhost:5173") // React frontend URL
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

// registers EmailSender so controllers can inject and use it to send emails
builder.Services.AddScoped<CivicFix.Api.Services.EmailSender>();

builder.Services.AddHostedService<CivicFix.Api.Services.LatePenaltyService>();//to run this when program starts

var app = builder.Build();//runnable object

app.UseHttpsRedirection();// Configure the HTTP request pipeline.


var wwwrootPath = Path.Combine(builder.Environment.ContentRootPath, "wwwroot");
Directory.CreateDirectory(Path.Combine(wwwrootPath, "uploads")); // creates both levels, no-op if present

app.UseStaticFiles();

app.UseCors("AllowReact"); // MOVED UP from below MapControllers()

app.UseAuthentication(); // reads the token from the request, validates it, extracts user info in controller

app.UseAuthorization();  // checks if that user is allowed to do this action depending on role :[Authorize(Roles = "Staff,Admin")]

app.MapControllers();//program here map controller with correct route

app.Run();//runs and start listening for request
// this page is like setting.py and manage.py{ here where app is configured and lanched}