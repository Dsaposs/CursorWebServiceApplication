using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using NotesApi.Configuration;
using NotesApi.Data;
using NotesApi.Models;
using NotesApi.Rulesets;
using NotesApi.Services;

var builder = WebApplication.CreateBuilder(args);
const string FrontendCorsPolicy = "Frontend";

builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection(JwtSettings.SectionName));

var jwtSettings = builder.Configuration
    .GetSection(JwtSettings.SectionName)
    .Get<JwtSettings>() ?? throw new InvalidOperationException("JWT settings are not configured.");

ValidateSecurityConfiguration(builder.Environment, builder.Configuration, jwtSettings);

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
builder.Services.AddSingleton<RulesetDefinitionValidator>();
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
    await ApplySchemaUpdatesAsync(db);
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
        var exceptionFeature = context.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>();
        var exception = exceptionFeature?.Error;

        var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
        logger.LogError(exception, "Unhandled exception for {Method} {Path}", context.Request.Method, context.Request.Path);

        context.Response.StatusCode = 500;
        context.Response.ContentType = "application/problem+json";

        var isDev = context.RequestServices.GetRequiredService<IWebHostEnvironment>().IsDevelopment();
        await context.Response.WriteAsJsonAsync(new
        {
            type = "https://tools.ietf.org/html/rfc7807",
            title = "An unexpected error occurred.",
            status = 500,
            detail = isDev
                ? $"{exception?.GetType().Name}: {exception?.Message}"
                : "The server encountered an error and could not complete your request.",
        });
    });
});

app.UseCors(FrontendCorsPolicy);
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health");

app.Run();

static void ValidateSecurityConfiguration(IHostEnvironment environment, IConfiguration configuration, JwtSettings jwtSettings)
{
    if (string.IsNullOrWhiteSpace(jwtSettings.Key) || jwtSettings.Key.Length < 32)
    {
        throw new InvalidOperationException("Jwt:Key must be configured with at least 32 characters.");
    }

    if (!environment.IsProduction())
    {
        return;
    }

    var insecureJwtKeys = new[]
    {
        "TtrpgApi-Dev-Signing-Key-Min-32-Chars-Long!",
        "ChangeThisDockerJwtSigningKeyToARealLongRandomValue123!",
        "ChangeThisKubernetesJwtSigningKeyToARealLongRandomValue123!",
    };
    if (insecureJwtKeys.Contains(jwtSettings.Key) || jwtSettings.Key.StartsWith("ChangeThis", StringComparison.OrdinalIgnoreCase))
    {
        throw new InvalidOperationException("Production Jwt:Key must be supplied from a secret and cannot use a checked-in placeholder.");
    }

    var adminPassword = configuration["Seed:AdminPassword"];
    if (string.IsNullOrWhiteSpace(adminPassword) || adminPassword == "Password1" || adminPassword.Length < 12)
    {
        throw new InvalidOperationException("Production Seed:AdminPassword must be supplied from a secret and cannot use the development default.");
    }
}

static async Task ApplySchemaUpdatesAsync(ApplicationDbContext db)
{
    // Adds columns introduced after the initial EnsureCreated; safe to run on every startup.
    var connection = db.Database.GetDbConnection();
    var wasOpen = connection.State == System.Data.ConnectionState.Open;
    if (!wasOpen)
        await connection.OpenAsync();

    var hasNpcVisibilitiesJson = false;
    var hasRulesetDefinitionJson = false;
    var hasCharacterClassKey = false;
    var hasActionKey = false;
    using (var pragma = connection.CreateCommand())
    {
        pragma.CommandText = "PRAGMA table_info('GameSessions')";
        using var reader = await pragma.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            if (reader["name"]?.ToString() == "NpcVisibilitiesJson")
            {
                hasNpcVisibilitiesJson = true;
                break;
            }
        }
    }

    using (var pragma = connection.CreateCommand())
    {
        pragma.CommandText = "PRAGMA table_info('Rulesets')";
        using var reader = await pragma.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            if (reader["name"]?.ToString() == "DefinitionJson")
            {
                hasRulesetDefinitionJson = true;
                break;
            }
        }
    }

    using (var pragma = connection.CreateCommand())
    {
        pragma.CommandText = "PRAGMA table_info('Characters')";
        using var reader = await pragma.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            if (reader["name"]?.ToString() == "ClassKey")
            {
                hasCharacterClassKey = true;
                break;
            }
        }
    }

    using (var pragma = connection.CreateCommand())
    {
        pragma.CommandText = "PRAGMA table_info('ActionRequests')";
        using var reader = await pragma.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            if (reader["name"]?.ToString() == "ActionKey")
            {
                hasActionKey = true;
                break;
            }
        }
    }

    if (!hasNpcVisibilitiesJson)
    {
        using var alter = connection.CreateCommand();
        alter.CommandText = "ALTER TABLE \"GameSessions\" ADD COLUMN \"NpcVisibilitiesJson\" TEXT DEFAULT '{}'";
        await alter.ExecuteNonQueryAsync();
    }

    if (!hasRulesetDefinitionJson)
    {
        using var alter = connection.CreateCommand();
        alter.CommandText = "ALTER TABLE \"Rulesets\" ADD COLUMN \"DefinitionJson\" TEXT DEFAULT '{}'";
        await alter.ExecuteNonQueryAsync();
    }

    if (!hasCharacterClassKey)
    {
        using var alter = connection.CreateCommand();
        alter.CommandText = "ALTER TABLE \"Characters\" ADD COLUMN \"ClassKey\" TEXT DEFAULT ''";
        await alter.ExecuteNonQueryAsync();
    }

    if (!hasActionKey)
    {
        using var alter = connection.CreateCommand();
        alter.CommandText = "ALTER TABLE \"ActionRequests\" ADD COLUMN \"ActionKey\" TEXT NULL";
        await alter.ExecuteNonQueryAsync();
    }

    if (!wasOpen)
        await connection.CloseAsync();
}

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
                  "gameValues": { "stress": 0 },
                  "experience": 0
                }
                """,
            DefinitionJson = """
                {
                  "schemaVersion": 1,
                  "code": "alien-rpg",
                  "displayName": "Alien RPG",
                  "description": "A horror sci-fi ruleset using attributes, skills, stress, and d6 dice pools.",
                  "diceNotation": "d6 dice pool",
                  "dice": [
                    { "key": "d6Pool", "label": "D6 Dice Pool", "notation": "attribute + skill + modifiers d6" }
                  ],
                  "character": {
                    "vitals": {
                      "health": { "label": "Health", "defaultMax": 10 },
                      "armor": { "label": "Armor", "default": 0 },
                      "experience": { "label": "Experience", "default": 0 }
                    },
                    "attributes": [
                      { "key": "strength", "label": "Strength", "default": 2, "min": 1, "max": 5 },
                      { "key": "agility", "label": "Agility", "default": 2, "min": 1, "max": 5 },
                      { "key": "wits", "label": "Wits", "default": 2, "min": 1, "max": 5 },
                      { "key": "empathy", "label": "Empathy", "default": 2, "min": 1, "max": 5 }
                    ],
                    "gameValues": [
                      { "key": "stress", "label": "Stress Level", "type": "number", "default": 0, "min": 0 }
                    ],
                    "classes": [
                      {
                        "key": "colonialMarine",
                        "label": "Colonial Marine",
                        "description": "Combat-trained military specialist.",
                        "availableSkills": [ "closeCombat", "rangedCombat", "mobility", "stamina" ],
                        "startingSkillPoints": 10
                      },
                      {
                        "key": "scientist",
                        "label": "Scientist",
                        "description": "Research and technical expert.",
                        "availableSkills": [ "observation", "survival", "comtech", "medicalAid" ],
                        "startingSkillPoints": 10
                      }
                    ],
                    "skills": [
                      { "key": "closeCombat", "label": "Close Combat", "attribute": "strength", "default": 0 },
                      { "key": "rangedCombat", "label": "Ranged Combat", "attribute": "agility", "default": 0 },
                      { "key": "mobility", "label": "Mobility", "attribute": "agility", "default": 0 },
                      { "key": "stamina", "label": "Stamina", "attribute": "strength", "default": 0 },
                      { "key": "observation", "label": "Observation", "attribute": "wits", "default": 0 },
                      { "key": "survival", "label": "Survival", "attribute": "wits", "default": 0 },
                      { "key": "comtech", "label": "Comtech", "attribute": "wits", "default": 0 },
                      { "key": "medicalAid", "label": "Medical Aid", "attribute": "empathy", "default": 0 }
                    ]
                  },
                  "actions": [
                    {
                      "key": "rangedAttack",
                      "label": "Ranged Attack",
                      "description": "Fire a ranged weapon at a target.",
                      "allowedClasses": [ "colonialMarine" ],
                      "roll": {
                        "dice": "d6Pool",
                        "attribute": "agility",
                        "skill": "rangedCombat",
                        "modifiers": [
                          { "source": "gameValue", "key": "stress", "dicePerPoint": 1 },
                          { "source": "equipment", "key": "gearBonus", "dicePerPoint": 1 }
                        ],
                        "successRule": "Each 6 is a success. Any 1 on stress dice may trigger panic."
                      }
                    },
                    {
                      "key": "observeThreat",
                      "label": "Observe Threat",
                      "description": "Spot danger, hidden motion, or environmental clues.",
                      "allowedClasses": [],
                      "roll": {
                        "dice": "d6Pool",
                        "attribute": "wits",
                        "skill": "observation",
                        "modifiers": [],
                        "successRule": "Each 6 is a success. Extra successes reveal more detail."
                      }
                    }
                  ],
                  "npcTemplates": []
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
            DefinitionJson = """
                {
                  "schemaVersion": 1,
                  "code": "dnd-5e",
                  "displayName": "Dungeons & Dragons",
                  "description": "Placeholder for a fantasy d20 ruleset.",
                  "diceNotation": "d20",
                  "dice": [{ "key": "d20", "label": "D20", "notation": "1d20 + modifiers" }],
                  "character": {
                    "vitals": {},
                    "attributes": [
                      { "key": "strength", "label": "Strength", "default": 10 },
                      { "key": "dexterity", "label": "Dexterity", "default": 10 },
                      { "key": "constitution", "label": "Constitution", "default": 10 },
                      { "key": "intelligence", "label": "Intelligence", "default": 10 },
                      { "key": "wisdom", "label": "Wisdom", "default": 10 },
                      { "key": "charisma", "label": "Charisma", "default": 10 }
                    ],
                    "gameValues": [],
                    "classes": [],
                    "skills": []
                  },
                  "actions": [],
                  "npcTemplates": []
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
            DefinitionJson = """
                {
                  "schemaVersion": 1,
                  "code": "pathfinder-2e",
                  "displayName": "Pathfinder",
                  "description": "Placeholder for a tactical fantasy d20 ruleset.",
                  "diceNotation": "d20",
                  "dice": [{ "key": "d20", "label": "D20", "notation": "1d20 + modifiers" }],
                  "character": {
                    "vitals": {},
                    "attributes": [
                      { "key": "strength", "label": "Strength", "default": 10 },
                      { "key": "dexterity", "label": "Dexterity", "default": 10 },
                      { "key": "constitution", "label": "Constitution", "default": 10 },
                      { "key": "intelligence", "label": "Intelligence", "default": 10 },
                      { "key": "wisdom", "label": "Wisdom", "default": 10 },
                      { "key": "charisma", "label": "Charisma", "default": 10 }
                    ],
                    "gameValues": [],
                    "classes": [],
                    "skills": []
                  },
                  "actions": [],
                  "npcTemplates": []
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
        existing.DefinitionJson = ruleset.DefinitionJson;
    }

    await db.SaveChangesAsync();
}
