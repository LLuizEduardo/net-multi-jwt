// Program.cs
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Configure app token validation (only accepts tokens from Gateway)
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = builder.Configuration["Jwt:AppIssuer"],
            ValidateAudience = true,
            ValidAudience = builder.Configuration["Jwt:AppAudience"],
            ValidateLifetime = true,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(builder.Configuration["Jwt:AppSecret"]))
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("InternalApi", policy =>
        policy.RequireClaim("api", "gateway"));
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

// Internal data endpoint
app.MapGet("/api/internal/orders", async (string userId) =>
{
    // In real implementation, query database
    var mockOrders = new List<Order>
    {
        new("1", userId, 100.50m, DateTime.UtcNow.AddDays(-1), "Shipped"),
        new("2", userId, 29.99m, DateTime.UtcNow.AddDays(-3), "Processing")
    };

    return Results.Ok(mockOrders);
})
.RequireAuthorization("InternalApi"); // Only allows Gateway's app token

app.Run();

// Model
record Order(string Id, string UserId, decimal Amount, DateTime OrderDate, string Status);