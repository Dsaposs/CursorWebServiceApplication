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
const string FrontendCorsPolicy = "Frontend";

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
builder.Services.AddCors(options =>
{
    var allowedOrigins = builder.Configuration
        .GetSection("Cors:AllowedOrigins")
        .Get<string[]>() ?? new[] { "http://localhost:3000" };

    options.AddPolicy(FrontendCorsPolicy, policy =>
        policy.WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod());
});
builder.Services.AddScoped<JwtTokenService>();
builder.Services.AddHealthChecks();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "TTRPG Table API", Version = "v1" });
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
    await SeedRulesetsAsync(db);

    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
    var adminPassword = builder.Configuration["Seed:AdminPassword"]
        ?? throw new InvalidOperationException("Seed:AdminPassword is not configured.");
    await SeedDefaultUsersAsync(roleManager, userManager, adminPassword);
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        context.Response.StatusCode = 500;
        context.Response.ContentType = "application/problem+json";
        await context.Response.WriteAsJsonAsync(new
        {
            type = "https://tools.ietf.org/html/rfc7807",
            title = "An unexpected error occurred.",
            status = 500,
            detail = "The server encountered an error and could not complete your request.",
        });
    });
});

app.UseCors(FrontendCorsPolicy);
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health");

app.Run();

static async Task SeedDefaultUsersAsync(RoleManager<IdentityRole> roleManager, UserManager<ApplicationUser> userManager, string adminPassword)
{
    const string adminRole = "Admin";
    const string userRole = "User";
    const string adminUserName = "admin";
    const string adminEmail = "admin@example.local";

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

static async Task SeedRulesetsAsync(ApplicationDbContext db)
{
    var rulesets = new[]
    {
        new Ruleset
        {
            Code = "alien-rpg",
            DisplayName = "Alien RPG",
            Description = "A horror sci-fi ruleset using attributes, skills, stress, and d6 dice pools.",
            DiceNotation = "d6 dice pool",
            IsPlaceholder = false,
            CharacterTemplateJson = """
                {
                  "attributes": { "strength": 2, "agility": 2, "wits": 2, "empathy": 2 },
                  "skills": { "closeCombat": 0, "rangedCombat": 0, "mobility": 0, "observation": 0, "survival": 0, "medicalAid": 0 },
                  "stress": 0
                }
                """,
        },
        new Ruleset
        {
            Code = "dnd-5e",
            DisplayName = "Dungeons & Dragons",
            Description = "Placeholder for a fantasy d20 ruleset.",
            DiceNotation = "d20",
            IsPlaceholder = true,
            CharacterTemplateJson = """
                {
                  "attributes": { "strength": 10, "dexterity": 10, "constitution": 10, "intelligence": 10, "wisdom": 10, "charisma": 10 },
                  "skills": {},
                  "level": 1
                }
                """,
        },
        new Ruleset
        {
            Code = "pathfinder-2e",
            DisplayName = "Pathfinder",
            Description = "Placeholder for a tactical fantasy d20 ruleset.",
            DiceNotation = "d20",
            IsPlaceholder = true,
            CharacterTemplateJson = """
                {
                  "attributes": { "strength": 10, "dexterity": 10, "constitution": 10, "intelligence": 10, "wisdom": 10, "charisma": 10 },
                  "skills": {},
                  "level": 1
                }
                """,
        },
    };

    foreach (var ruleset in rulesets)
    {
        var existing = await db.Rulesets.FindAsync(ruleset.Code);
        if (existing is null)
        {
            db.Rulesets.Add(ruleset);
            continue;
        }

        existing.DisplayName = ruleset.DisplayName;
        existing.Description = ruleset.Description;
        existing.DiceNotation = ruleset.DiceNotation;
        existing.IsPlaceholder = ruleset.IsPlaceholder;
        existing.CharacterTemplateJson = ruleset.CharacterTemplateJson;
    }

    await db.SaveChangesAsync();
}
