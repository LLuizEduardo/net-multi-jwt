// Program.cs
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Configure user token validation
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = builder.Configuration["Jwt:UserIssuer"],
            ValidateAudience = true,
            ValidAudience = builder.Configuration["Jwt:UserAudience"],
            ValidateLifetime = true,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(builder.Configuration["Jwt:UserSecret"]))
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHttpClient();

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();
app.UseSwagger();
app.UseSwaggerUI();


// Example: Get user dashboard data
// Corrected dashboard endpoint
app.MapGet("/api/dashboard", async (HttpContext context, IHttpClientFactory clientFactory) =>
{
    //var userId = context.User.Claims.FirstOrDefault(c => c.Type == "sub")?.Value;

    //if (string.IsNullOrEmpty(userId))
    //    return Results.Unauthorized();

    // Get orders from repository
    var appToken = GenerateAppToken(builder.Configuration);
    var client = clientFactory.CreateClient();
    client.DefaultRequestHeaders.Authorization = new("Bearer", appToken);

    var ordersResponse = await client.GetAsync(
        $"{builder.Configuration["RepositoryApi:BaseUrl"]}/api/internal/orders?userId=2");
    //$"{builder.Configuration["RepositoryApi:BaseUrl"]}/api/internal/orders?userId={userId}");

    if (!ordersResponse.IsSuccessStatusCode)
        return Results.Problem("Failed to retrieve data");

    var orders = await ordersResponse.Content.ReadFromJsonAsync<List<Order>>() ?? new List<Order>();

    // Fixed view model creation
    var dashboardData = new DashboardViewModel(
        RecentOrders: orders.Take(5).ToList(),
        OrderCount: orders.Count,
        StatusSummary: CalculateStatusSummary(orders)
    );

    return Results.Ok(dashboardData);
});
//.RequireAuthorization();

// Corrected helper method
Dictionary<string, int> CalculateStatusSummary(List<Order> orders)
{
    return orders
        .GroupBy(o => o.Status)
        .ToDictionary(
            g => g.Key,
            g => g.Count());
}

app.Run();

// Helper methods
string GenerateAppToken(IConfiguration config)
{
    var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(config["Jwt:AppSecret"]));
    var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

    var token = new JwtSecurityToken(
        issuer: config["Jwt:AppIssuer"],
        audience: config["Jwt:AppAudience"],
        expires: DateTime.UtcNow.AddMinutes(5),
        claims: new[] { new Claim("api", "gateway") },
        signingCredentials: credentials
    );

    return new JwtSecurityTokenHandler().WriteToken(token);
}

// Models
record Order(string Id, string UserId, decimal Amount, DateTime OrderDate, string Status);
record DashboardViewModel(List<Order> RecentOrders, int OrderCount, Dictionary<string, int> StatusSummary);