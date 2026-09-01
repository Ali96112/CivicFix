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
    // ══════════════════════════════════════════════════════════════════════
    // FIXED — THE JSON NAMING MISMATCH.
    //
    // By default ASP.NET Core renames every C# property to camelCase on its way
    // out. So this in the controller:
    //     return Ok(new { Report = report, Assignments = assignments });
    // arrived in React as { "report": ..., "assignments": ... } — lowercase.
    // That is why ReportDetail crashed with
    //     Cannot read properties of undefined (reading 'CategoryName')
    // It asked for data.Report, but the JSON only had data.report.
    //
    // The confusing part: the LISTS always worked. That is because Dapper's
    // `dynamic` rows are dictionaries, and the camelCase rule applies to real
    // class properties, NOT to dictionary keys. So rpt_Id, CategoryName and
    // MunicipalityName came through untouched, while anything returned from a
    // real C# class or `new { ... }` got renamed. Two different conventions in
    // the same API — impossible to guess from the React side.
    //
    // Setting the policy to null turns the renaming off, so what you write in
    // C# is exactly what React receives, everywhere.
    // ══════════════════════════════════════════════════════════════════════
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = null;
    });

builder.Services.AddScoped<SqlConnection>(sp =>
    new SqlConnection(builder.Configuration.GetConnectionString("DefaultConnection")));
// AddScoped means a new connection is created per request and closed when the request finishes
// this is what injected in _connection: database setting in appsettings


builder.Services.AddDbContext<AppDbContext>(options =>  //Registering AppDbContext but EF core read it at migration time
    // appdbcontext used for migration to update/insert
    options.UseSqlServer(
        //tell EF to Use SQL Server as the database engine
        builder.Configuration.GetConnectionString("DefaultConnection"),
        //tell EF to Read the database address from appsettings.json
        x => x.UseNetTopologySuite()
        // this line activate the bridge between the EF and installed NetTopologySuite library
    ));
//"My application will have controllers, use SQL Server"


builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters//sets validation rule for every token want to pass
        {
            ValidateIssuer = true,           // check token came from "CivicFix"
            ValidateAudience = true,         // check token is meant for "CivicFixUsers"
            ValidateLifetime = true,         // check token hasn't expired
            ValidateIssuerSigningKey = true, // check token signature wasn't tampered with

            ValidIssuer = builder.Configuration["Jwt:Issuer"],       // reads "CivicFix" from appsettings.json
            ValidAudience = builder.Configuration["Jwt:Audience"],   // reads "CivicFixUsers" from appsettings.json

            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]))
            // reads secret key from appsettings.json
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


// RESTORED — allows UsersController to receive EmailSender through its constructor
builder.Services.AddScoped<CivicFix.Api.Services.EmailSender>();

// RESTORED — starts LatePenaltyService when the API starts
builder.Services.AddHostedService<CivicFix.Api.Services.LatePenaltyService>();


var app = builder.Build();//runnable object


app.UseHttpsRedirection();// Configure the HTTP request pipeline.


// ADDED — serve uploaded photos as real files over http.
//
// UploadsController saves images into wwwroot/uploads. UseStaticFiles is what
// makes that folder reachable from a browser: wwwroot/uploads/abc.jpg becomes
// http://localhost:5140/uploads/abc.jpg, which is the URL stored in
// rpt_ReportedPhotoUrl and put into an <img src="..."> by React.
//
// The folder is created first because UseStaticFiles throws on startup when
// wwwroot does not exist — and it will not exist the very first time you run
// this after pulling these changes.
var wwwrootPath = Path.Combine(builder.Environment.ContentRootPath, "wwwroot");

Directory.CreateDirectory(
    Path.Combine(wwwrootPath, "uploads")
); // creates both levels, no-op if present

app.UseStaticFiles(); // must come before MapControllers, same as UseCors below


// FIXED — ORDER BUG. UseCors("AllowReact") used to sit AFTER MapControllers(),
// which means it was never reached: middleware runs top to bottom, and
// MapControllers() ends the pipeline. React on :5173 was getting
// "blocked by CORS policy" errors on every call, especially on the
// browser's OPTIONS preflight for PUT/POST with an Authorization header.
// CORS must come BEFORE UseAuthentication / UseAuthorization / MapControllers.

app.UseCors("AllowReact");


app.UseAuthentication();
// reads the token from the request, validates it, extracts user info in controller

app.UseAuthorization();
// checks if that user is allowed to do this action depending on role:
// [Authorize(Roles = "Staff,Admin")]

app.MapControllers();//program here map controller with correct route

app.Run();//runs and start listening for request

// this page is like settings.py and manage.py
// here where app is configured and launched






//"ConnectionStrings": {
//    "DefaultConnection": "Server=(localdb)\\MSSQLLocalDB;Database=CivicFixDb;Trusted_Connection=True;TrustServerCertificate=True;"
  //},






