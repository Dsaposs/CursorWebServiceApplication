using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using NotesApi.Models;

namespace NotesApi.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
    {
    }

    public DbSet<Ruleset> Rulesets => Set<Ruleset>();
    public DbSet<Game> Games => Set<Game>();
    public DbSet<GameParticipant> GameParticipants => Set<GameParticipant>();
    public DbSet<Character> Characters => Set<Character>();
    public DbSet<NpcOrMonster> NpcsAndMonsters => Set<NpcOrMonster>();
    public DbSet<GameSession> GameSessions => Set<GameSession>();
    public DbSet<ActionRequest> ActionRequests => Set<ActionRequest>();
    public DbSet<ActionResolution> ActionResolutions => Set<ActionResolution>();
    public DbSet<InitiativeEntry> InitiativeEntries => Set<InitiativeEntry>();
    public DbSet<ActionRollPrompt> ActionRollPrompts => Set<ActionRollPrompt>();
    public DbSet<SessionRollPrompt> SessionRollPrompts => Set<SessionRollPrompt>();
    public DbSet<CombatEncounter> CombatEncounters => Set<CombatEncounter>();
    public DbSet<SessionNote> SessionNotes => Set<SessionNote>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Ruleset>(entity =>
        {
            entity.HasKey(r => r.Code);
            entity.Property(r => r.Code).HasMaxLength(50);
            entity.Property(r => r.DisplayName).HasMaxLength(120).IsRequired();
            entity.Property(r => r.Description).HasMaxLength(500);
            entity.Property(r => r.DiceNotation).HasMaxLength(80);
            entity.Property(r => r.CharacterTemplateJson).IsRequired();
            entity.Property(r => r.DefinitionJson).IsRequired();
        });

        builder.Entity<Game>(entity =>
        {
            entity.Property(g => g.Name).HasMaxLength(160).IsRequired();
            entity.Property(g => g.Description).HasMaxLength(1000);
            entity.Property(g => g.InviteCode).HasMaxLength(32).IsRequired();
            entity.HasIndex(g => g.InviteCode).IsUnique();
            entity.HasIndex(g => g.Name).IsUnique();
            entity.HasIndex(g => new { g.DmUserId, g.CreatedAt });
            entity.HasOne(g => g.DmUser)
                .WithMany(u => u.GamesHosted)
                .HasForeignKey(g => g.DmUserId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(g => g.Ruleset)
                .WithMany(r => r.Games)
                .HasForeignKey(g => g.RulesetCode)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<GameParticipant>(entity =>
        {
            entity.Property(p => p.DisplayName).HasMaxLength(160).IsRequired();
            entity.Property(p => p.JoinToken).HasMaxLength(80).IsRequired();
            entity.HasIndex(p => p.JoinToken).IsUnique();
            entity.HasIndex(p => new { p.GameId, p.DisplayName });
            entity.HasOne(p => p.Game)
                .WithMany()
                .HasForeignKey(p => p.GameId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(p => p.Character)
                .WithMany(c => c.Participants)
                .HasForeignKey(p => p.CharacterId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<Character>(entity =>
        {
            entity.Property(c => c.Name).HasMaxLength(160).IsRequired();
            entity.Property(c => c.PlayerName).HasMaxLength(160);
            entity.Property(c => c.ClassKey).HasMaxLength(80);
            entity.HasIndex(c => new { c.GameId, c.Name }).IsUnique();
            entity.HasOne(c => c.Game)
                .WithMany(g => g.Characters)
                .HasForeignKey(c => c.GameId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<NpcOrMonster>(entity =>
        {
            entity.Property(n => n.Name).HasMaxLength(160).IsRequired();
            entity.Property(n => n.Kind).HasMaxLength(80);
            entity.HasIndex(n => new { n.GameId, n.Name });
            entity.HasOne(n => n.Game)
                .WithMany(g => g.NpcsAndMonsters)
                .HasForeignKey(n => n.GameId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<GameSession>(entity =>
        {
            entity.Property(s => s.JoinCode).HasMaxLength(32).IsRequired();
            entity.HasIndex(s => s.JoinCode).IsUnique();
            entity.HasIndex(s => new { s.GameId, s.StartedAt });
            entity.HasOne(s => s.Game)
                .WithMany(g => g.Sessions)
                .HasForeignKey(s => s.GameId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(s => s.ActiveCombatEncounter)
                .WithMany()
                .HasForeignKey(s => s.ActiveCombatEncounterId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<ActionRequest>(entity =>
        {
            entity.Property(a => a.ActorName).HasMaxLength(160).IsRequired();
            entity.Property(a => a.ActionText).HasMaxLength(240).IsRequired();
            entity.Property(a => a.ActionKey).HasMaxLength(80);
            entity.Property(a => a.TargetName).HasMaxLength(160);
            entity.Property(a => a.Description).HasMaxLength(1000);
            entity.HasIndex(a => new { a.SessionId, a.Sequence }).IsUnique();
            entity.HasOne(a => a.Session)
                .WithMany(s => s.Actions)
                .HasForeignKey(a => a.SessionId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(a => a.CombatEncounter)
                .WithMany(e => e.Actions)
                .HasForeignKey(a => a.CombatEncounterId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<ActionResolution>(entity =>
        {
            entity.Property(r => r.ResolutionText).IsRequired();
            entity.HasIndex(r => r.ActionRequestId).IsUnique();
            entity.HasOne(r => r.ActionRequest)
                .WithOne(a => a.Resolution)
                .HasForeignKey<ActionResolution>(r => r.ActionRequestId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<InitiativeEntry>(entity =>
        {
            entity.Property(i => i.CombatantName).HasMaxLength(160).IsRequired();
            entity.HasIndex(i => new { i.SessionId, i.SortOrder }).IsUnique();
            entity.HasOne(i => i.Session)
                .WithMany(s => s.InitiativeEntries)
                .HasForeignKey(i => i.SessionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<CombatEncounter>(entity =>
        {
            entity.HasIndex(e => new { e.SessionId, e.Sequence }).IsUnique();
            entity.HasOne(e => e.Session)
                .WithMany(s => s.CombatEncounters)
                .HasForeignKey(e => e.SessionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<ActionRollPrompt>(entity =>
        {
            entity.Property(p => p.PromptLabel).HasMaxLength(200);
            entity.Property(p => p.CheckMode).HasMaxLength(20).IsRequired();
            entity.Property(p => p.ResultKind).HasMaxLength(20).IsRequired();
            entity.Property(p => p.ActionKey).HasMaxLength(80);
            entity.Property(p => p.SkillKey).HasMaxLength(80);
            entity.Property(p => p.AttributeKey).HasMaxLength(80);
            entity.Property(p => p.CustomCheckText).HasMaxLength(240);
            entity.Property(p => p.RollSummary).HasMaxLength(500);
            entity.HasIndex(p => new { p.ActionRequestId, p.Status });
            entity.HasOne(p => p.ActionRequest)
                .WithMany(a => a.RollPrompts)
                .HasForeignKey(p => p.ActionRequestId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(p => p.TargetCharacter)
                .WithMany()
                .HasForeignKey(p => p.TargetCharacterId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<SessionNote>(entity =>
        {
            entity.Property(n => n.OwnerKind).HasMaxLength(16).IsRequired();
            entity.Property(n => n.OwnerId).HasMaxLength(128).IsRequired();
            entity.Property(n => n.Content).IsRequired();
            entity.HasIndex(n => new { n.SessionId, n.OwnerKind, n.OwnerId }).IsUnique();
            entity.HasIndex(n => new { n.OwnerKind, n.OwnerId, n.UpdatedAt });
            entity.HasOne(n => n.Session)
                .WithMany(s => s.SessionNotes)
                .HasForeignKey(n => n.SessionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<SessionRollPrompt>(entity =>
        {
            entity.Property(p => p.PromptLabel).HasMaxLength(200);
            entity.Property(p => p.CheckMode).HasMaxLength(20).IsRequired();
            entity.Property(p => p.ResultKind).HasMaxLength(20).IsRequired();
            entity.Property(p => p.ActionKey).HasMaxLength(80);
            entity.Property(p => p.SkillKey).HasMaxLength(80);
            entity.Property(p => p.AttributeKey).HasMaxLength(80);
            entity.Property(p => p.CustomCheckText).HasMaxLength(240);
            entity.Property(p => p.RollSummary).HasMaxLength(500);
            entity.HasIndex(p => new { p.SessionId, p.Status });
            entity.HasOne(p => p.Session)
                .WithMany(s => s.SessionRollPrompts)
                .HasForeignKey(p => p.SessionId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(p => p.TargetCharacter)
                .WithMany()
                .HasForeignKey(p => p.TargetCharacterId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(p => p.ActionRequest)
                .WithMany()
                .HasForeignKey(p => p.ActionRequestId)
                .OnDelete(DeleteBehavior.SetNull);
        });
    }
}
