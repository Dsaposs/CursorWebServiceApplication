using System.Text;
using System.Threading.RateLimiting;
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
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.AllowedForNewUsers = true;
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
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("auth", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                Window = TimeSpan.FromMinutes(1),
                PermitLimit = 10,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0,
            }));
});

builder.Services.AddScoped<JwtTokenService>();
builder.Services.AddSingleton<RulesetDefinitionValidator>();
builder.Services.AddHostedService<SessionTimeoutService>();
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
else
{
    app.UseHsts();
    app.UseHttpsRedirection();
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
app.UseRateLimiter();
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

    var hasRollPromptsTable = false;
    using (var tables = connection.CreateCommand())
    {
        tables.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name='ActionRollPrompts'";
        hasRollPromptsTable = await tables.ExecuteScalarAsync() is not null;
    }

    var hasCombatEncountersTable = false;
    using (var tables = connection.CreateCommand())
    {
        tables.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name='CombatEncounters'";
        hasCombatEncountersTable = await tables.ExecuteScalarAsync() is not null;
    }

    if (!hasCombatEncountersTable)
    {
        using var create = connection.CreateCommand();
        create.CommandText = """
            CREATE TABLE "CombatEncounters" (
                "Id" TEXT NOT NULL CONSTRAINT "PK_CombatEncounters" PRIMARY KEY,
                "SessionId" TEXT NOT NULL,
                "Sequence" INTEGER NOT NULL,
                "StartedAt" TEXT NOT NULL,
                "EndedAt" TEXT NULL,
                CONSTRAINT "FK_CombatEncounters_GameSessions_SessionId" FOREIGN KEY ("SessionId") REFERENCES "GameSessions" ("Id") ON DELETE CASCADE
            );
            CREATE UNIQUE INDEX "IX_CombatEncounters_SessionId_Sequence" ON "CombatEncounters" ("SessionId", "Sequence");
            """;
        await create.ExecuteNonQueryAsync();
    }

    var hasActiveCombatEncounterId = false;
    using (var pragma = connection.CreateCommand())
    {
        pragma.CommandText = "PRAGMA table_info('GameSessions')";
        using var reader = await pragma.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            if (reader["name"]?.ToString() == "ActiveCombatEncounterId")
            {
                hasActiveCombatEncounterId = true;
                break;
            }
        }
    }

    if (!hasActiveCombatEncounterId)
    {
        using var alter = connection.CreateCommand();
        alter.CommandText = "ALTER TABLE \"GameSessions\" ADD COLUMN \"ActiveCombatEncounterId\" TEXT NULL";
        await alter.ExecuteNonQueryAsync();
    }

    var hasCombatEncounterIdOnActions = false;
    using (var pragma = connection.CreateCommand())
    {
        pragma.CommandText = "PRAGMA table_info('ActionRequests')";
        using var reader = await pragma.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            if (reader["name"]?.ToString() == "CombatEncounterId")
            {
                hasCombatEncounterIdOnActions = true;
                break;
            }
        }
    }

    if (!hasCombatEncounterIdOnActions)
    {
        using var alter = connection.CreateCommand();
        alter.CommandText = "ALTER TABLE \"ActionRequests\" ADD COLUMN \"CombatEncounterId\" TEXT NULL";
        await alter.ExecuteNonQueryAsync();
    }

    if (!hasRollPromptsTable)
    {
        using var create = connection.CreateCommand();
        create.CommandText = """
            CREATE TABLE "ActionRollPrompts" (
                "Id" TEXT NOT NULL CONSTRAINT "PK_ActionRollPrompts" PRIMARY KEY,
                "ActionRequestId" TEXT NOT NULL,
                "TargetCharacterId" TEXT NOT NULL,
                "PromptLabel" TEXT NULL,
                "CheckMode" TEXT NOT NULL,
                "ActionKey" TEXT NULL,
                "SkillKey" TEXT NULL,
                "AttributeKey" TEXT NULL,
                "CustomCheckText" TEXT NULL,
                "Status" INTEGER NOT NULL,
                "RollSummary" TEXT NULL,
                "CreatedAt" TEXT NOT NULL,
                "CompletedAt" TEXT NULL,
                CONSTRAINT "FK_ActionRollPrompts_ActionRequests_ActionRequestId" FOREIGN KEY ("ActionRequestId") REFERENCES "ActionRequests" ("Id") ON DELETE CASCADE,
                CONSTRAINT "FK_ActionRollPrompts_Characters_TargetCharacterId" FOREIGN KEY ("TargetCharacterId") REFERENCES "Characters" ("Id") ON DELETE CASCADE
            );
            CREATE INDEX "IX_ActionRollPrompts_ActionRequestId_Status" ON "ActionRollPrompts" ("ActionRequestId", "Status");
            """;
        await create.ExecuteNonQueryAsync();
    }

    var hasResolutionOutcome = false;
    using (var pragma = connection.CreateCommand())
    {
        pragma.CommandText = "PRAGMA table_info('ActionResolutions')";
        using var reader = await pragma.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            if (reader["name"]?.ToString() == "Outcome")
            {
                hasResolutionOutcome = true;
                break;
            }
        }
    }

    if (!hasResolutionOutcome)
    {
        using var alter = connection.CreateCommand();
        alter.CommandText = "ALTER TABLE \"ActionResolutions\" ADD COLUMN \"Outcome\" INTEGER NULL";
        await alter.ExecuteNonQueryAsync();
    }

    var hasSessionRollPromptsTable = false;
    using (var tables = connection.CreateCommand())
    {
        tables.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name='SessionRollPrompts'";
        hasSessionRollPromptsTable = await tables.ExecuteScalarAsync() is not null;
    }

    if (!hasSessionRollPromptsTable)
    {
        using var create = connection.CreateCommand();
        create.CommandText = """
            CREATE TABLE "SessionRollPrompts" (
                "Id" TEXT NOT NULL CONSTRAINT "PK_SessionRollPrompts" PRIMARY KEY,
                "SessionId" TEXT NOT NULL,
                "TargetCharacterId" TEXT NOT NULL,
                "PromptLabel" TEXT NULL,
                "CheckMode" TEXT NOT NULL,
                "ActionKey" TEXT NULL,
                "SkillKey" TEXT NULL,
                "AttributeKey" TEXT NULL,
                "CustomCheckText" TEXT NULL,
                "Status" INTEGER NOT NULL,
                "RollSummary" TEXT NULL,
                "CreatedAt" TEXT NOT NULL,
                "CompletedAt" TEXT NULL,
                "ActionRequestId" TEXT NULL,
                "SkillCheckBatchId" TEXT NULL,
                CONSTRAINT "FK_SessionRollPrompts_GameSessions_SessionId" FOREIGN KEY ("SessionId") REFERENCES "GameSessions" ("Id") ON DELETE CASCADE,
                CONSTRAINT "FK_SessionRollPrompts_Characters_TargetCharacterId" FOREIGN KEY ("TargetCharacterId") REFERENCES "Characters" ("Id") ON DELETE CASCADE
            );
            CREATE INDEX "IX_SessionRollPrompts_SessionId_Status" ON "SessionRollPrompts" ("SessionId", "Status");
            """;
        await create.ExecuteNonQueryAsync();
    }

    var hasSessionPromptActionRequestId = false;
    using (var pragma = connection.CreateCommand())
    {
        pragma.CommandText = "PRAGMA table_info('SessionRollPrompts')";
        using var reader = await pragma.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            if (reader["name"]?.ToString() == "ActionRequestId")
            {
                hasSessionPromptActionRequestId = true;
                break;
            }
        }
    }

    if (!hasSessionPromptActionRequestId)
    {
        using var alter = connection.CreateCommand();
        alter.CommandText = "ALTER TABLE \"SessionRollPrompts\" ADD COLUMN \"ActionRequestId\" TEXT NULL";
        await alter.ExecuteNonQueryAsync();
    }

    var hasSessionPromptBatchId = false;
    using (var pragma = connection.CreateCommand())
    {
        pragma.CommandText = "PRAGMA table_info('SessionRollPrompts')";
        using var reader = await pragma.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            if (reader["name"]?.ToString() == "SkillCheckBatchId")
            {
                hasSessionPromptBatchId = true;
                break;
            }
        }
    }

    if (!hasSessionPromptBatchId)
    {
        using var alter = connection.CreateCommand();
        alter.CommandText = "ALTER TABLE \"SessionRollPrompts\" ADD COLUMN \"SkillCheckBatchId\" TEXT NULL";
        await alter.ExecuteNonQueryAsync();
    }

    var hasActionSkillCheckBatchId = false;
    using (var pragma = connection.CreateCommand())
    {
        pragma.CommandText = "PRAGMA table_info('ActionRequests')";
        using var reader = await pragma.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            if (reader["name"]?.ToString() == "SkillCheckBatchId")
            {
                hasActionSkillCheckBatchId = true;
                break;
            }
        }
    }

    if (!hasActionSkillCheckBatchId)
    {
        using var alter = connection.CreateCommand();
        alter.CommandText = """
            ALTER TABLE "ActionRequests" ADD COLUMN "SkillCheckBatchId" TEXT NULL;
            ALTER TABLE "ActionRequests" ADD COLUMN "SkillCheckGroupLabel" TEXT NULL;
            """;
        await alter.ExecuteNonQueryAsync();
    }

    var hasActionRollPromptResultKind = false;
    using (var pragma = connection.CreateCommand())
    {
        pragma.CommandText = "PRAGMA table_info('ActionRollPrompts')";
        using var reader = await pragma.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            if (reader["name"]?.ToString() == "ResultKind")
            {
                hasActionRollPromptResultKind = true;
                break;
            }
        }
    }

    if (!hasActionRollPromptResultKind)
    {
        using var alter = connection.CreateCommand();
        alter.CommandText = """
            ALTER TABLE "ActionRollPrompts" ADD COLUMN "ResultKind" TEXT NOT NULL DEFAULT 'PassFail';
            """;
        await alter.ExecuteNonQueryAsync();
    }

    var hasSessionRollPromptResultKind = false;
    using (var pragma = connection.CreateCommand())
    {
        pragma.CommandText = "PRAGMA table_info('SessionRollPrompts')";
        using var reader = await pragma.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            if (reader["name"]?.ToString() == "ResultKind")
            {
                hasSessionRollPromptResultKind = true;
                break;
            }
        }
    }

    if (!hasSessionRollPromptResultKind)
    {
        using var alter = connection.CreateCommand();
        alter.CommandText = """
            ALTER TABLE "SessionRollPrompts" ADD COLUMN "ResultKind" TEXT NOT NULL DEFAULT 'PassFail';
            """;
        await alter.ExecuteNonQueryAsync();
    }

    var hasSessionNotesTable = false;
    using (var tables = connection.CreateCommand())
    {
        tables.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name='SessionNotes'";
        hasSessionNotesTable = await tables.ExecuteScalarAsync() is not null;
    }

    if (!hasSessionNotesTable)
    {
        using var create = connection.CreateCommand();
        create.CommandText = """
            CREATE TABLE "SessionNotes" (
                "Id" TEXT NOT NULL CONSTRAINT "PK_SessionNotes" PRIMARY KEY,
                "SessionId" TEXT NOT NULL,
                "OwnerKind" TEXT NOT NULL,
                "OwnerId" TEXT NOT NULL,
                "Content" TEXT NOT NULL DEFAULT '',
                "CreatedAt" TEXT NOT NULL,
                "UpdatedAt" TEXT NOT NULL,
                CONSTRAINT "FK_SessionNotes_GameSessions_SessionId" FOREIGN KEY ("SessionId") REFERENCES "GameSessions" ("Id") ON DELETE CASCADE
            );
            CREATE UNIQUE INDEX "IX_SessionNotes_SessionId_OwnerKind_OwnerId" ON "SessionNotes" ("SessionId", "OwnerKind", "OwnerId");
            CREATE INDEX "IX_SessionNotes_OwnerKind_OwnerId_UpdatedAt" ON "SessionNotes" ("OwnerKind", "OwnerId", "UpdatedAt");
            """;
        await create.ExecuteNonQueryAsync();
    }

    var hasAttributesJsonColumn = false;
    using (var pragma = connection.CreateCommand())
    {
        pragma.CommandText = "PRAGMA table_info('Characters')";
        using var reader = await pragma.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            if (reader["name"]?.ToString() == "AttributesJson")
            {
                hasAttributesJsonColumn = true;
                break;
            }
        }
    }

    if (hasAttributesJsonColumn)
    {
        await MigrateLegacyCharacterStatsAsync(connection);
        using var dropAttributes = connection.CreateCommand();
        dropAttributes.CommandText = "ALTER TABLE \"Characters\" DROP COLUMN \"AttributesJson\"";
        await dropAttributes.ExecuteNonQueryAsync();
        using var dropSkills = connection.CreateCommand();
        dropSkills.CommandText = "ALTER TABLE \"Characters\" DROP COLUMN \"SkillsJson\"";
        await dropSkills.ExecuteNonQueryAsync();
    }

    var hasCharacterStatusEffectsJson = false;
    using (var pragma = connection.CreateCommand())
    {
        pragma.CommandText = "PRAGMA table_info('Characters')";
        using var reader = await pragma.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            if (reader["name"]?.ToString() == "StatusEffectsJson")
            {
                hasCharacterStatusEffectsJson = true;
                break;
            }
        }
    }

    if (!hasCharacterStatusEffectsJson)
    {
        using var alter = connection.CreateCommand();
        alter.CommandText = "ALTER TABLE \"Characters\" ADD COLUMN \"StatusEffectsJson\" TEXT NOT NULL DEFAULT '[]'";
        await alter.ExecuteNonQueryAsync();
    }

    var hasNpcStatusEffectsJson = false;
    using (var pragma = connection.CreateCommand())
    {
        pragma.CommandText = "PRAGMA table_info('NpcsAndMonsters')";
        using var reader = await pragma.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            if (reader["name"]?.ToString() == "StatusEffectsJson")
            {
                hasNpcStatusEffectsJson = true;
                break;
            }
        }
    }

    if (!hasNpcStatusEffectsJson)
    {
        using var alter = connection.CreateCommand();
        alter.CommandText = "ALTER TABLE \"NpcsAndMonsters\" ADD COLUMN \"StatusEffectsJson\" TEXT NOT NULL DEFAULT '[]'";
        await alter.ExecuteNonQueryAsync();
    }

    var hasActionPromptGuidanceText = false;
    using (var pragma = connection.CreateCommand())
    {
        pragma.CommandText = "PRAGMA table_info('ActionRollPrompts')";
        using var reader = await pragma.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            if (reader["name"]?.ToString() == "GuidanceText")
            {
                hasActionPromptGuidanceText = true;
                break;
            }
        }
    }

    if (!hasActionPromptGuidanceText)
    {
        using var alter = connection.CreateCommand();
        alter.CommandText = "ALTER TABLE \"ActionRollPrompts\" ADD COLUMN \"GuidanceText\" TEXT NULL";
        await alter.ExecuteNonQueryAsync();
    }

    var hasSessionPromptGuidanceText = false;
    using (var pragma = connection.CreateCommand())
    {
        pragma.CommandText = "PRAGMA table_info('SessionRollPrompts')";
        using var reader = await pragma.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            if (reader["name"]?.ToString() == "GuidanceText")
            {
                hasSessionPromptGuidanceText = true;
                break;
            }
        }
    }

    if (!hasSessionPromptGuidanceText)
    {
        using var alter = connection.CreateCommand();
        alter.CommandText = "ALTER TABLE \"SessionRollPrompts\" ADD COLUMN \"GuidanceText\" TEXT NULL";
        await alter.ExecuteNonQueryAsync();
    }

    await EnsureColumnAsync(connection, "ActionRequests", "RollChainStateJson", "TEXT NULL");
    await EnsureColumnAsync(connection, "ActionRequests", "PendingChainEffectsJson", "TEXT NOT NULL DEFAULT '[]'");
    await EnsureColumnAsync(connection, "ActionRollPrompts", "RollResultJson", "TEXT NULL");
    await EnsureColumnAsync(connection, "ActionRollPrompts", "ChainStepKey", "TEXT NULL");
    await EnsureColumnAsync(connection, "ActionRollPrompts", "AutoResolveOutcome", "TEXT NULL");
    await EnsureColumnAsync(connection, "ActionRollPrompts", "Dc", "INTEGER NULL");
    await EnsureColumnAsync(connection, "ActionRollPrompts", "DmRolled", "INTEGER NOT NULL DEFAULT 0");
    await EnsureColumnAsync(connection, "SessionRollPrompts", "RollResultJson", "TEXT NULL");
    await EnsureColumnAsync(connection, "InitiativeEntries", "InitiativeScore", "INTEGER NOT NULL DEFAULT 0");
    await EnsureColumnAsync(connection, "CombatEncounters", "Round", "INTEGER NOT NULL DEFAULT 1");
    await EnsureColumnAsync(connection, "CombatEncounters", "PromptedTurnCharacterId", "TEXT NULL");

    if (!wasOpen)
        await connection.CloseAsync();
}

static async Task EnsureColumnAsync(System.Data.Common.DbConnection connection, string table, string column, string sqlType)
{
    var hasColumn = false;
    using (var pragma = connection.CreateCommand())
    {
        pragma.CommandText = $"PRAGMA table_info('{table}')";
        using var reader = await pragma.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            if (reader["name"]?.ToString() == column)
            {
                hasColumn = true;
                break;
            }
        }
    }

    if (!hasColumn)
    {
        using var alter = connection.CreateCommand();
        alter.CommandText = $"ALTER TABLE \"{table}\" ADD COLUMN \"{column}\" {sqlType}";
        await alter.ExecuteNonQueryAsync();
    }
}

static async Task MigrateLegacyCharacterStatsAsync(System.Data.Common.DbConnection connection)
{
    var updates = new List<(string Id, string RulesetDataJson)>();
    using (var select = connection.CreateCommand())
    {
        select.CommandText = """
            SELECT "Id", "AttributesJson", "SkillsJson", "RulesetDataJson"
            FROM "Characters"
            """;
        using var reader = await select.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var id = reader.GetString(0);
            var attributesJson = reader.IsDBNull(1) ? "{}" : reader.GetString(1);
            var skillsJson = reader.IsDBNull(2) ? "{}" : reader.GetString(2);
            var rulesetDataJson = reader.IsDBNull(3) ? "{}" : reader.GetString(3);
            var merged = RulesetCharacterData.MergeLegacyStats(rulesetDataJson, attributesJson, skillsJson);
            updates.Add((id, merged));
        }
    }

    var now = DateTime.UtcNow.ToString("O");
    foreach (var (id, rulesetDataJson) in updates)
    {
        using var update = connection.CreateCommand();
        update.CommandText = """
            UPDATE "Characters"
            SET "RulesetDataJson" = $rulesetDataJson, "UpdatedAt" = $updatedAt
            WHERE "Id" = $id
            """;
        var rulesetParam = update.CreateParameter();
        rulesetParam.ParameterName = "$rulesetDataJson";
        rulesetParam.Value = rulesetDataJson;
        update.Parameters.Add(rulesetParam);
        var updatedAtParam = update.CreateParameter();
        updatedAtParam.ParameterName = "$updatedAt";
        updatedAtParam.Value = now;
        update.Parameters.Add(updatedAtParam);
        var idParam = update.CreateParameter();
        idParam.ParameterName = "$id";
        idParam.Value = id;
        update.Parameters.Add(idParam);
        await update.ExecuteNonQueryAsync();
    }
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
    var jsonOptions = new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web);

    foreach (var code in RulesetSeedCatalog.Codes)
    {
        var definitionJson = RulesetDefinitionLoader.LoadLatestDefinition(code);
        var definition = System.Text.Json.JsonSerializer.Deserialize<RulesetDefinition>(definitionJson, jsonOptions)
            ?? throw new InvalidOperationException($"Could not deserialize ruleset definition for '{code}'.");

        var ruleset = new Ruleset
        {
            Code = definition.Code,
            DisplayName = definition.DisplayName,
            Description = definition.Description,
            DiceNotation = definition.DiceNotation,
            IsPlaceholder = RulesetSeedCatalog.IsPlaceholder(code),
            CharacterTemplateJson = RulesetDefinitionLoader.LoadCharacterTemplate(code),
            DefinitionJson = definitionJson,
        };

        var existing = await db.Rulesets.FindAsync(ruleset.Code);
        if (existing is null)
        {
            db.Rulesets.Add(ruleset);
            continue;
        }

        // Skip update when nothing has changed — avoids a write on every startup.
        if (existing.DefinitionJson == ruleset.DefinitionJson
            && existing.CharacterTemplateJson == ruleset.CharacterTemplateJson
            && existing.DisplayName == ruleset.DisplayName)
        {
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
