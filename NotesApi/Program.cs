using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using NotesApi.Configuration;
using NotesApi.Data;
using NotesApi.Models;
using NotesApi.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection(JwtSettings.SectionName));

var jwtSettings = builder.Configuration
    .GetSection(JwtSettings.SectionName)
    .Get<JwtSettings>() ?? throw new InvalidOperationException("JWT settings are not configured.");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services
    .AddIdentity<ApplicationUser, IdentityRole>(options =>
    {
        options.User.RequireUniqueEmail = true;
        options.Password.RequiredLength = 7;
        options.Password.RequireUppercase = true;
        options.Password.RequireDigit = true;
        options.Password.RequireLowercase = false;
        options.Password.RequireNonAlphanumeric = false;
    })
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSettings.Issuer,
            ValidAudience = jwtSettings.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.Key)),
            ClockSkew = TimeSpan.FromMinutes(1),
            RoleClaimType = System.Security.Claims.ClaimTypes.Role,
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddScoped<JwtTokenService>();
builder.Services.AddHealthChecks();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "Notes API", Version = "v1" });
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
    });
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" },
            },
            Array.Empty<string>()
        },
    });
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    db.Database.EnsureCreated();
    EnsureUserCounterColumns(db);

    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
    await SeedDefaultUsersAsync(roleManager, userManager);
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseDefaultFiles();
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health");

app.Run();

static void EnsureUserCounterColumns(ApplicationDbContext db)
{
    AddColumnIfMissing(db, "NotesCreatedCount");
    AddColumnIfMissing(db, "NotesDeletedCount");
    db.Database.ExecuteSqlRaw("""
        UPDATE AspNetUsers
        SET NotesCreatedCount = (
            SELECT COUNT(*)
            FROM Notes
            WHERE Notes.UserId = AspNetUsers.Id
        )
        WHERE NotesCreatedCount < (
            SELECT COUNT(*)
            FROM Notes
            WHERE Notes.UserId = AspNetUsers.Id
        )
        """);
}

static void AddColumnIfMissing(ApplicationDbContext db, string columnName)
{
    if (UserColumnExists(db, columnName))
    {
        return;
    }

    if (columnName == "NotesCreatedCount")
    {
        db.Database.ExecuteSqlRaw("ALTER TABLE AspNetUsers ADD COLUMN NotesCreatedCount INTEGER NOT NULL DEFAULT 0");
    }
    else if (columnName == "NotesDeletedCount")
    {
        db.Database.ExecuteSqlRaw("ALTER TABLE AspNetUsers ADD COLUMN NotesDeletedCount INTEGER NOT NULL DEFAULT 0");
    }
}

static bool UserColumnExists(ApplicationDbContext db, string columnName)
{
    var connection = db.Database.GetDbConnection();
    var shouldClose = connection.State == System.Data.ConnectionState.Closed;
    if (shouldClose) connection.Open();

    try
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM pragma_table_info('AspNetUsers') WHERE name = $columnName";
        var parameter = command.CreateParameter();
        parameter.ParameterName = "$columnName";
        parameter.Value = columnName;
        command.Parameters.Add(parameter);
        return Convert.ToInt32(command.ExecuteScalar()) > 0;
    }
    finally
    {
        if (shouldClose) connection.Close();
    }
}

static async Task SeedDefaultUsersAsync(RoleManager<IdentityRole> roleManager, UserManager<ApplicationUser> userManager)
{
    const string adminRole = "Admin";
    const string userRole = "User";
    const string adminUserName = "admin";
    const string adminEmail = "admin@example.local";
    const string adminPassword = "Password1";

    foreach (var role in new[] { adminRole, userRole })
    {
        if (!await roleManager.RoleExistsAsync(role))
        {
            await roleManager.CreateAsync(new IdentityRole(role));
        }
    }

    var admin = await userManager.FindByNameAsync(adminUserName);
    if (admin is null)
    {
        admin = new ApplicationUser
        {
            UserName = adminUserName,
            Email = adminEmail,
            EmailConfirmed = true,
        };

        var createResult = await userManager.CreateAsync(admin, adminPassword);
        if (!createResult.Succeeded)
        {
            throw new InvalidOperationException($"Could not seed admin user: {string.Join("; ", createResult.Errors.Select(error => error.Description))}");
        }
    }
    else if (admin.Email != adminEmail)
    {
        admin.Email = adminEmail;
        admin.NormalizedEmail = adminEmail.ToUpperInvariant();
        admin.EmailConfirmed = true;
        await userManager.UpdateAsync(admin);
    }

    if (!await userManager.IsInRoleAsync(admin, adminRole))
    {
        await userManager.AddToRoleAsync(admin, adminRole);
    }

    if (await userManager.IsInRoleAsync(admin, userRole))
    {
        await userManager.RemoveFromRoleAsync(admin, userRole);
    }

    foreach (var user in userManager.Users.Where(user => user.UserName != adminUserName))
    {
        if (!await userManager.IsInRoleAsync(user, userRole))
        {
            await userManager.AddToRoleAsync(user, userRole);
        }
    }
}
