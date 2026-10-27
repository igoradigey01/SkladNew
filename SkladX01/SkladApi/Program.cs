using EmailService;
using HRDB.Data;
using HRDB.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SkladApi.Services;
using SkladDB.Data;
using SkladDB.Services;

var builder = WebApplication.CreateBuilder(args);

var skladConnection = RequireConnection(builder.Configuration, "SkladDb");
var hrConnection = RequireConnection(builder.Configuration, "HRDb");

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserService, HttpCurrentUserService>();

builder.Services.Configure<EmailSettings>(
    builder.Configuration.GetSection(EmailSettings.SectionName));
builder.Services.AddScoped<IEmailSender, EmailSender>();

builder.Services.AddDbContext<HRContext>(options =>
    options.UseSqlServer(hrConnection));

builder.Services.AddDbContext<SkladContext>(options =>
    options.UseSqlServer(skladConnection));

builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
    {
        options.SignIn.RequireConfirmedAccount = false;
        options.User.RequireUniqueEmail = false;
    })
    .AddEntityFrameworkStores<HRContext>()
    .AddDefaultTokenProviders();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

static string RequireConnection(IConfiguration configuration, string name)
{
    var value = configuration.GetConnectionString(name);

    if (string.IsNullOrWhiteSpace(value))
    {
        throw new InvalidOperationException(
            $"Строка подключения '{name}' не задана. " +
            "Задайте её через dotnet user-secrets или переменные окружения.");
    }

    return value;
}
