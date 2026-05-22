using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NotesApi.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AspNetRoles",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    NormalizedName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetRoles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUsers",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    UserName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    NormalizedUserName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    Email = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    NormalizedEmail = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    EmailConfirmed = table.Column<bool>(nullable: false),
                    PasswordHash = table.Column<string>(type: "TEXT", nullable: true),
                    SecurityStamp = table.Column<string>(type: "TEXT", nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "TEXT", nullable: true),
                    PhoneNumber = table.Column<string>(type: "TEXT", nullable: true),
                    PhoneNumberConfirmed = table.Column<bool>(nullable: false),
                    TwoFactorEnabled = table.Column<bool>(nullable: false),
                    LockoutEnd = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    LockoutEnabled = table.Column<bool>(nullable: false),
                    AccessFailedCount = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUsers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Rulesets",
                columns: table => new
                {
                    Code = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    DisplayName = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    DiceNotation = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    IsPlaceholder = table.Column<bool>(nullable: false),
                    CharacterTemplateJson = table.Column<string>(type: "TEXT", nullable: false),
                    DefinitionJson = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Rulesets", x => x.Code);
                });

            migrationBuilder.CreateTable(
                name: "AspNetRoleClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    RoleId = table.Column<string>(type: "TEXT", nullable: false),
                    ClaimType = table.Column<string>(type: "TEXT", nullable: true),
                    ClaimValue = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetRoleClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetRoleClaims_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<string>(type: "TEXT", nullable: false),
                    ClaimType = table.Column<string>(type: "TEXT", nullable: true),
                    ClaimValue = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetUserClaims_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserLogins",
                columns: table => new
                {
                    LoginProvider = table.Column<string>(type: "TEXT", nullable: false),
                    ProviderKey = table.Column<string>(type: "TEXT", nullable: false),
                    ProviderDisplayName = table.Column<string>(type: "TEXT", nullable: true),
                    UserId = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserLogins", x => new { x.LoginProvider, x.ProviderKey });
                    table.ForeignKey(
                        name: "FK_AspNetUserLogins_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserRoles",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "TEXT", nullable: false),
                    RoleId = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserRoles", x => new { x.UserId, x.RoleId });
                    table.ForeignKey(
                        name: "FK_AspNetUserRoles_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AspNetUserRoles_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserTokens",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "TEXT", nullable: false),
                    LoginProvider = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Value = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserTokens", x => new { x.UserId, x.LoginProvider, x.Name });
                    table.ForeignKey(
                        name: "FK_AspNetUserTokens_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RefreshTokens",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserId = table.Column<string>(type: "TEXT", nullable: false),
                    TokenHash = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    ReplacedById = table.Column<Guid>(type: "TEXT", nullable: true),
                    IsRevoked = table.Column<bool>(nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RefreshTokens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RefreshTokens_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Games",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    DmUserId = table.Column<string>(type: "TEXT", nullable: false),
                    RulesetCode = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 160, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    InviteCode = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Games", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Games_AspNetUsers_DmUserId",
                        column: x => x.DmUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Games_Rulesets_RulesetCode",
                        column: x => x.RulesetCode,
                        principalTable: "Rulesets",
                        principalColumn: "Code",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Campaigns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    OwnerId = table.Column<string>(type: "TEXT", nullable: false),
                    GameId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Campaigns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Campaigns_AspNetUsers_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Campaigns_Games_GameId",
                        column: x => x.GameId,
                        principalTable: "Games",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Characters",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    GameId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 160, nullable: false),
                    PlayerName = table.Column<string>(type: "TEXT", maxLength: 160, nullable: false),
                    MaxHealth = table.Column<int>(type: "INTEGER", nullable: false),
                    Health = table.Column<int>(type: "INTEGER", nullable: false),
                    Armor = table.Column<int>(type: "INTEGER", nullable: false),
                    InventoryJson = table.Column<string>(type: "TEXT", nullable: false),
                    RulesetDataJson = table.Column<string>(type: "TEXT", nullable: false),
                    StatusEffectsJson = table.Column<string>(type: "TEXT", nullable: false),
                    ClassKey = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Characters", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Characters_Games_GameId",
                        column: x => x.GameId,
                        principalTable: "Games",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "NpcsAndMonsters",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    GameId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 160, nullable: false),
                    Kind = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    MaxHealth = table.Column<int>(type: "INTEGER", nullable: false),
                    Health = table.Column<int>(type: "INTEGER", nullable: false),
                    Armor = table.Column<int>(type: "INTEGER", nullable: false),
                    StatBlockJson = table.Column<string>(type: "TEXT", nullable: false),
                    StatusEffectsJson = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NpcsAndMonsters", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NpcsAndMonsters_Games_GameId",
                        column: x => x.GameId,
                        principalTable: "Games",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CampaignMembers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    CampaignId = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserId = table.Column<string>(type: "TEXT", nullable: false),
                    JoinedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CampaignMembers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CampaignMembers_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CampaignMembers_Campaigns_CampaignId",
                        column: x => x.CampaignId,
                        principalTable: "Campaigns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GameParticipants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    GameId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CharacterId = table.Column<Guid>(type: "TEXT", nullable: false),
                    DisplayName = table.Column<string>(type: "TEXT", maxLength: 160, nullable: false),
                    JoinToken = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastSeenAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GameParticipants", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GameParticipants_Characters_CharacterId",
                        column: x => x.CharacterId,
                        principalTable: "Characters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_GameParticipants_Games_GameId",
                        column: x => x.GameId,
                        principalTable: "Games",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ActionRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    SessionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ActorCharacterId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ActorNpcId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ActorName = table.Column<string>(type: "TEXT", maxLength: 160, nullable: false),
                    ActionText = table.Column<string>(type: "TEXT", maxLength: 240, nullable: false),
                    ActionKey = table.Column<string>(type: "TEXT", maxLength: 80, nullable: true),
                    TargetCharacterId = table.Column<Guid>(type: "TEXT", nullable: true),
                    TargetNpcId = table.Column<Guid>(type: "TEXT", nullable: true),
                    TargetName = table.Column<string>(type: "TEXT", maxLength: 160, nullable: true),
                    Description = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    Sequence = table.Column<int>(type: "INTEGER", nullable: false),
                    CombatEncounterId = table.Column<Guid>(type: "TEXT", nullable: true),
                    SkillCheckBatchId = table.Column<Guid>(type: "TEXT", nullable: true),
                    SkillCheckGroupLabel = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    RollChainStateJson = table.Column<string>(type: "TEXT", nullable: true),
                    PendingChainEffectsJson = table.Column<string>(type: "TEXT", nullable: false),
                    ParentActionId = table.Column<Guid>(type: "TEXT", nullable: true),
                    FollowUpType = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                    ChainStep = table.Column<int>(type: "INTEGER", nullable: true),
                    SessionModeAtSubmit = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                    CombatRound = table.Column<int>(type: "INTEGER", nullable: true),
                    DmDifficultyModifier = table.Column<int>(type: "INTEGER", nullable: true),
                    EffectiveDc = table.Column<int>(type: "INTEGER", nullable: true),
                    RollMode = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    RollDataJson = table.Column<string>(type: "TEXT", nullable: true),
                    FlavourText = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    SubmittedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    PublishedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ResolvedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActionRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ActionRequests_ActionRequests_ParentActionId",
                        column: x => x.ParentActionId,
                        principalTable: "ActionRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ActionResolutions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ActionRequestId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ResolutionText = table.Column<string>(type: "TEXT", nullable: false),
                    RollSummary = table.Column<string>(type: "TEXT", nullable: true),
                    AdditionalActions = table.Column<string>(type: "TEXT", nullable: true),
                    StatChangesJson = table.Column<string>(type: "TEXT", nullable: false),
                    Outcome = table.Column<int>(type: "INTEGER", nullable: true),
                    PublishedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActionResolutions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ActionResolutions_ActionRequests_ActionRequestId",
                        column: x => x.ActionRequestId,
                        principalTable: "ActionRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ActionRollPrompts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ActionRequestId = table.Column<Guid>(type: "TEXT", nullable: false),
                    TargetCharacterId = table.Column<Guid>(type: "TEXT", nullable: false),
                    PromptLabel = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    GuidanceText = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    CheckMode = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    ResultKind = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    ActionKey = table.Column<string>(type: "TEXT", maxLength: 80, nullable: true),
                    SkillKey = table.Column<string>(type: "TEXT", maxLength: 80, nullable: true),
                    AttributeKey = table.Column<string>(type: "TEXT", maxLength: 80, nullable: true),
                    CustomCheckText = table.Column<string>(type: "TEXT", maxLength: 240, nullable: true),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    RollSummary = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    RollResultJson = table.Column<string>(type: "TEXT", nullable: true),
                    ChainStepKey = table.Column<string>(type: "TEXT", maxLength: 80, nullable: true),
                    AutoResolveOutcome = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                    Dc = table.Column<int>(type: "INTEGER", nullable: true),
                    DmRolled = table.Column<bool>(nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActionRollPrompts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ActionRollPrompts_ActionRequests_ActionRequestId",
                        column: x => x.ActionRequestId,
                        principalTable: "ActionRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ActionRollPrompts_Characters_TargetCharacterId",
                        column: x => x.TargetCharacterId,
                        principalTable: "Characters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CombatEncounters",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    SessionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Sequence = table.Column<int>(type: "INTEGER", nullable: false),
                    Round = table.Column<int>(type: "INTEGER", nullable: false),
                    PromptedTurnCharacterId = table.Column<Guid>(type: "TEXT", nullable: true),
                    StartedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    EndedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CombatEncounters", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "GameSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    GameId = table.Column<Guid>(type: "TEXT", nullable: false),
                    JoinCode = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    IsActive = table.Column<bool>(nullable: false),
                    State = table.Column<int>(type: "INTEGER", nullable: false),
                    Version = table.Column<int>(type: "INTEGER", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    EndedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    NpcVisibilitiesJson = table.Column<string>(type: "TEXT", nullable: false),
                    DiceRollMode = table.Column<int>(type: "INTEGER", nullable: false),
                    ActiveTurnParticipantId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ActiveCombatEncounterId = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GameSessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GameSessions_CombatEncounters_ActiveCombatEncounterId",
                        column: x => x.ActiveCombatEncounterId,
                        principalTable: "CombatEncounters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_GameSessions_Games_GameId",
                        column: x => x.GameId,
                        principalTable: "Games",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "InitiativeEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    SessionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CombatantType = table.Column<int>(type: "INTEGER", nullable: false),
                    CombatantId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CombatantName = table.Column<string>(type: "TEXT", maxLength: 160, nullable: false),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    InitiativeScore = table.Column<int>(type: "INTEGER", nullable: false),
                    IsCurrentTurn = table.Column<bool>(nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InitiativeEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InitiativeEntries_GameSessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "GameSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ScheduledSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    CampaignId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    ScheduledAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    DurationMinutes = table.Column<int>(type: "INTEGER", nullable: false),
                    Recurrence = table.Column<int>(type: "INTEGER", nullable: false),
                    RecurrenceCron = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    LinkedSessionId = table.Column<Guid>(type: "TEXT", nullable: true),
                    IsCancelled = table.Column<bool>(nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScheduledSessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ScheduledSessions_Campaigns_CampaignId",
                        column: x => x.CampaignId,
                        principalTable: "Campaigns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ScheduledSessions_GameSessions_LinkedSessionId",
                        column: x => x.LinkedSessionId,
                        principalTable: "GameSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "SessionNotes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    SessionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    OwnerKind = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    OwnerId = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    Content = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SessionNotes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SessionNotes_GameSessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "GameSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SessionRollPrompts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    SessionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    TargetCharacterId = table.Column<Guid>(type: "TEXT", nullable: false),
                    PromptLabel = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    GuidanceText = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    CheckMode = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    ResultKind = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    ActionKey = table.Column<string>(type: "TEXT", maxLength: 80, nullable: true),
                    SkillKey = table.Column<string>(type: "TEXT", maxLength: 80, nullable: true),
                    AttributeKey = table.Column<string>(type: "TEXT", maxLength: 80, nullable: true),
                    CustomCheckText = table.Column<string>(type: "TEXT", maxLength: 240, nullable: true),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    RollSummary = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    RollResultJson = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    SkillCheckBatchId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ActionRequestId = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SessionRollPrompts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SessionRollPrompts_ActionRequests_ActionRequestId",
                        column: x => x.ActionRequestId,
                        principalTable: "ActionRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_SessionRollPrompts_Characters_TargetCharacterId",
                        column: x => x.TargetCharacterId,
                        principalTable: "Characters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SessionRollPrompts_GameSessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "GameSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ActionRequests_CombatEncounterId",
                table: "ActionRequests",
                column: "CombatEncounterId");

            migrationBuilder.CreateIndex(
                name: "IX_ActionRequests_ParentActionId",
                table: "ActionRequests",
                column: "ParentActionId");

            migrationBuilder.CreateIndex(
                name: "IX_ActionRequests_SessionId_Sequence",
                table: "ActionRequests",
                columns: new[] { "SessionId", "Sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ActionResolutions_ActionRequestId",
                table: "ActionResolutions",
                column: "ActionRequestId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ActionRollPrompts_ActionRequestId_Status",
                table: "ActionRollPrompts",
                columns: new[] { "ActionRequestId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ActionRollPrompts_TargetCharacterId",
                table: "ActionRollPrompts",
                column: "TargetCharacterId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetRoleClaims_RoleId",
                table: "AspNetRoleClaims",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "RoleNameIndex",
                table: "AspNetRoles",
                column: "NormalizedName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserClaims_UserId",
                table: "AspNetUserClaims",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserLogins_UserId",
                table: "AspNetUserLogins",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserRoles_RoleId",
                table: "AspNetUserRoles",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "EmailIndex",
                table: "AspNetUsers",
                column: "NormalizedEmail");

            migrationBuilder.CreateIndex(
                name: "UserNameIndex",
                table: "AspNetUsers",
                column: "NormalizedUserName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CampaignMembers_CampaignId_UserId",
                table: "CampaignMembers",
                columns: new[] { "CampaignId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CampaignMembers_UserId",
                table: "CampaignMembers",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Campaigns_GameId",
                table: "Campaigns",
                column: "GameId");

            migrationBuilder.CreateIndex(
                name: "IX_Campaigns_OwnerId_CreatedAt",
                table: "Campaigns",
                columns: new[] { "OwnerId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Characters_GameId_Name",
                table: "Characters",
                columns: new[] { "GameId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CombatEncounters_SessionId_Sequence",
                table: "CombatEncounters",
                columns: new[] { "SessionId", "Sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GameParticipants_CharacterId",
                table: "GameParticipants",
                column: "CharacterId");

            migrationBuilder.CreateIndex(
                name: "IX_GameParticipants_GameId_DisplayName",
                table: "GameParticipants",
                columns: new[] { "GameId", "DisplayName" });

            migrationBuilder.CreateIndex(
                name: "IX_GameParticipants_JoinToken",
                table: "GameParticipants",
                column: "JoinToken",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Games_DmUserId_CreatedAt",
                table: "Games",
                columns: new[] { "DmUserId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Games_InviteCode",
                table: "Games",
                column: "InviteCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Games_Name",
                table: "Games",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Games_RulesetCode",
                table: "Games",
                column: "RulesetCode");

            migrationBuilder.CreateIndex(
                name: "IX_GameSessions_ActiveCombatEncounterId",
                table: "GameSessions",
                column: "ActiveCombatEncounterId");

            migrationBuilder.CreateIndex(
                name: "IX_GameSessions_GameId_StartedAt",
                table: "GameSessions",
                columns: new[] { "GameId", "StartedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_GameSessions_JoinCode",
                table: "GameSessions",
                column: "JoinCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InitiativeEntries_SessionId_SortOrder",
                table: "InitiativeEntries",
                columns: new[] { "SessionId", "SortOrder" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NpcsAndMonsters_GameId_Name",
                table: "NpcsAndMonsters",
                columns: new[] { "GameId", "Name" });

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_TokenHash",
                table: "RefreshTokens",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_UserId_IsRevoked_ExpiresAt",
                table: "RefreshTokens",
                columns: new[] { "UserId", "IsRevoked", "ExpiresAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ScheduledSessions_CampaignId_ScheduledAt",
                table: "ScheduledSessions",
                columns: new[] { "CampaignId", "ScheduledAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ScheduledSessions_LinkedSessionId",
                table: "ScheduledSessions",
                column: "LinkedSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_SessionNotes_OwnerKind_OwnerId_UpdatedAt",
                table: "SessionNotes",
                columns: new[] { "OwnerKind", "OwnerId", "UpdatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_SessionNotes_SessionId_OwnerKind_OwnerId",
                table: "SessionNotes",
                columns: new[] { "SessionId", "OwnerKind", "OwnerId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SessionRollPrompts_ActionRequestId",
                table: "SessionRollPrompts",
                column: "ActionRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_SessionRollPrompts_SessionId_Status",
                table: "SessionRollPrompts",
                columns: new[] { "SessionId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_SessionRollPrompts_TargetCharacterId",
                table: "SessionRollPrompts",
                column: "TargetCharacterId");

            migrationBuilder.AddForeignKey(
                name: "FK_ActionRequests_CombatEncounters_CombatEncounterId",
                table: "ActionRequests",
                column: "CombatEncounterId",
                principalTable: "CombatEncounters",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_ActionRequests_GameSessions_SessionId",
                table: "ActionRequests",
                column: "SessionId",
                principalTable: "GameSessions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_CombatEncounters_GameSessions_SessionId",
                table: "CombatEncounters",
                column: "SessionId",
                principalTable: "GameSessions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_GameSessions_CombatEncounters_ActiveCombatEncounterId",
                table: "GameSessions");

            migrationBuilder.DropTable(
                name: "ActionResolutions");

            migrationBuilder.DropTable(
                name: "ActionRollPrompts");

            migrationBuilder.DropTable(
                name: "AspNetRoleClaims");

            migrationBuilder.DropTable(
                name: "AspNetUserClaims");

            migrationBuilder.DropTable(
                name: "AspNetUserLogins");

            migrationBuilder.DropTable(
                name: "AspNetUserRoles");

            migrationBuilder.DropTable(
                name: "AspNetUserTokens");

            migrationBuilder.DropTable(
                name: "CampaignMembers");

            migrationBuilder.DropTable(
                name: "GameParticipants");

            migrationBuilder.DropTable(
                name: "InitiativeEntries");

            migrationBuilder.DropTable(
                name: "NpcsAndMonsters");

            migrationBuilder.DropTable(
                name: "RefreshTokens");

            migrationBuilder.DropTable(
                name: "ScheduledSessions");

            migrationBuilder.DropTable(
                name: "SessionNotes");

            migrationBuilder.DropTable(
                name: "SessionRollPrompts");

            migrationBuilder.DropTable(
                name: "AspNetRoles");

            migrationBuilder.DropTable(
                name: "Campaigns");

            migrationBuilder.DropTable(
                name: "ActionRequests");

            migrationBuilder.DropTable(
                name: "Characters");

            migrationBuilder.DropTable(
                name: "CombatEncounters");

            migrationBuilder.DropTable(
                name: "GameSessions");

            migrationBuilder.DropTable(
                name: "Games");

            migrationBuilder.DropTable(
                name: "AspNetUsers");

            migrationBuilder.DropTable(
                name: "Rulesets");
        }
    }
}
