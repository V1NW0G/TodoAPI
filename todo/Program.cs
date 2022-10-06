using System.Collections.Generic;
using System.Reflection;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using static System.Net.Mime.MediaTypeNames;
using Microsoft.AspNetCore.Authorization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

var builder = WebApplication.CreateBuilder(args);

////////////////auth/////////
var securityScheme = new OpenApiSecurityScheme()
{
    Name = "Authorization",
    Type = SecuritySchemeType.ApiKey,
    Scheme = "Bearer",
    BearerFormat = "JWT",
    In = ParameterLocation.Header,
    Description = "Paste the generated message from login bearer + {token} to here ",
};

var securityReq = new OpenApiSecurityRequirement()
{
    {
        new OpenApiSecurityScheme
        {
            Reference = new OpenApiReference
            {
                Type = ReferenceType.SecurityScheme,
                Id = "Bearer"
            }
        },
        new string[] {}
    }
};


var info = new OpenApiInfo()
{
    Version = "v1",
    Title = "Todo App",
    Description = "SleekFlow Coding Test",
};

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(option =>
{
    option.SwaggerDoc("v1", info);
    option.AddSecurityDefinition("Bearer", securityScheme);
    option.AddSecurityRequirement(securityReq);
});

builder.Services.AddAuthentication(option =>
{
    option.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    option.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    option.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
}).AddJwtBearer(option =>
{
    option.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"],
        ValidAudience = builder.Configuration["Jwt:Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"])),        
        ValidateAudience = true,
        ValidateLifetime = false,
        ValidateIssuerSigningKey = true
    };
});

builder.Services.AddAuthorization();
builder.Services.AddEndpointsApiExplorer();


builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddDbContext<TodoDb>(opt => opt.UseInMemoryDatabase("TodoList"));
var app = builder.Build();



/////////Login///////////
app.MapPost("/api/security/getToken", [AllowAnonymous] (UserDto user) =>
{

    if (user.UserName == "admin" && user.Password == "admin")
    {
        var issuer = builder.Configuration["Jwt:Issuer"];
        var audience = builder.Configuration["Jwt:Audience"];
        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]));
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);
        var jwtTokenHandler = new JwtSecurityTokenHandler();

        var key = Encoding.ASCII.GetBytes(builder.Configuration["Jwt:Key"]);
        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(new[]
            {
                new Claim("Id", "1"),
                new Claim(JwtRegisteredClaimNames.Sub, user.UserName),
                new Claim(JwtRegisteredClaimNames.Email, user.UserName),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            }),
    
            Expires = DateTime.UtcNow.AddHours(6),
            Audience = audience,
            Issuer = issuer,
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha512Signature)
        };

        var token = jwtTokenHandler.CreateToken(tokenDescriptor);

        var jwtToken = jwtTokenHandler.WriteToken(token);

        return Results.Ok("bearer " + jwtToken);
      
        
    }
    return Results.Unauthorized();

}).WithTags("1.Login");

/////////////////CRUD////////////////////////
app.MapGet("/api/post-all", [Authorize] async (TodoDb db) => await db.Todos.ToListAsync()).WithTags("CRUD");

app.MapPost("/api/create", [Authorize] async (Todo todo, TodoDb db) => {
    db.Todos.Add(todo);
    await db.SaveChangesAsync();
    return Results.Created($"/api/{todo.Id}", todo);
}).WithTags("CRUD");

app.MapPut("/api/edit{id}", [Authorize] async (int id, Todo inputTodo, TodoDb db) => {
    var todo = await db.Todos.FindAsync(id);
    if (todo == null) return Results.NotFound();
    todo.Name = inputTodo.Name;
    todo.Status = inputTodo.Status;
    todo.Duedate = inputTodo.Duedate;
    todo.Description = inputTodo.Description;
    await db.SaveChangesAsync();
    return Results.NoContent();
}).WithTags("CRUD");

app.MapDelete("/api/remove{id}", [Authorize] async (int id, TodoDb db) => {
    if (await db.Todos.FindAsync(id) is Todo todo)
    {
        db.Todos.Remove(todo);
        await db.SaveChangesAsync();
        return Results.Ok(todo);
    }
    return Results.NotFound();
}).WithTags("CRUD");

////////////////Filtering/////////////////
app.MapGet("/api/search/{query}", [Authorize] (string query, TodoDb db) =>
{
    var _selectedTodos = db.Todos.Where(x => x.Name.ToLower().Contains(query.ToLower())
    || x.Description.ToLower().Contains(query.ToLower())
    || x.Duedate.ToString().Contains(query.ToLower())
    ).ToList();

    return _selectedTodos.Count>0? Results.Ok(_selectedTodos)
    :Results.NotFound();

}).Produces<List<Todo>>(StatusCodes.Status200OK).WithName("Search").WithTags("Filtering");

app.MapGet("/api/search-complete", [Authorize] async (TodoDb db) => await db.Todos.Where(t => t.Status).ToListAsync()).WithTags("Filtering");

app.MapGet("/api/search{id}", [Authorize] async (int id, TodoDb db) => await db.Todos.FindAsync(id)
    is Todo todo ? Results.Ok(todo)
    : Results.NotFound()).WithTags("Filtering");

///////////////Sorting////////////////
app.MapGet("/api/sort", [Authorize] (PagingData pageData) => $"SortBy:{pageData.SortBy}, SortDirection:{pageData.SortDirection}, CurrentPage:{pageData.CurrentPage}").WithTags("Sorting");


if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
app.UseAuthentication();
app.UseAuthorization();
app.Run();

public class PagingData
{
    public string? SortBy { get; init; }
    public SortDirection SortDirection { get; init; }
    public int CurrentPage { get; init; } = 1;

    public static ValueTask<PagingData?> BindAsync(HttpContext context, ParameterInfo parameter)
    {
        const string sortByKey = "sortBy";
        const string sortDirectionKey = "sortDir";
        const string currentPageKey = "page";

        Enum.TryParse<SortDirection>(context.Request.Query[sortDirectionKey], ignoreCase: true, out var sortDirection);
        int.TryParse(context.Request.Query[currentPageKey], out var page);
        page = page == 0 ? 1 : page;

        var result = new PagingData
        {
            SortBy = context.Request.Query[sortByKey],
            SortDirection = sortDirection,
            CurrentPage = page
        };

        return ValueTask.FromResult<PagingData?>(result);
    }
}

public enum SortDirection
{
    Default,
    Asc,
    Desc
}

record UserDto(string UserName, string Password);
