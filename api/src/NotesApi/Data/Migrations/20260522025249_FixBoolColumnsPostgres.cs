using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NotesApi.Migrations
{
    /// <summary>
    /// Converts all bool columns that were created as INTEGER (SQLite type annotation leaked
    /// into early migrations) to proper BOOLEAN type in PostgreSQL.
    ///
    /// The migration is a no-op on SQLite — SQLite stores booleans as 0/1 integers and
    /// EF Core's SQLite provider handles the conversion automatically.
    ///
    /// On PostgreSQL, each ALTER uses a USING clause so existing 0/1 integer values are
    /// converted correctly to false/true. The IF EXISTS guard on each column makes this
    /// migration safely re-runnable and no-op on databases already created with boolean columns.
    /// </summary>
    public partial class FixBoolColumnsPostgres : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            if (migrationBuilder.ActiveProvider != "Npgsql.EntityFrameworkCore.PostgreSQL")
                return;

            // Each statement is wrapped in a DO block that first checks the column's
            // current data type. If it is already 'boolean', the ALTER is skipped.
            // This makes the migration safe for fresh installs AND existing installs.
            var boolColumns = new[]
            {
                ("AspNetUsers",      "EmailConfirmed"),
                ("AspNetUsers",      "PhoneNumberConfirmed"),
                ("AspNetUsers",      "TwoFactorEnabled"),
                ("AspNetUsers",      "LockoutEnabled"),
                ("Rulesets",         "IsPlaceholder"),
                ("RefreshTokens",    "IsRevoked"),
                ("ActionRollPrompts","DmRolled"),
                ("GameSessions",     "IsActive"),
                ("InitiativeEntries","IsCurrentTurn"),
                ("ScheduledSessions","IsCancelled"),
            };

            foreach (var (table, column) in boolColumns)
            {
                migrationBuilder.Sql($@"
DO $$
BEGIN
  IF EXISTS (
    SELECT 1 FROM information_schema.columns
    WHERE table_name = '{table}'
      AND column_name = '{column}'
      AND data_type   = 'integer'
  ) THEN
    ALTER TABLE ""{table}"" ALTER COLUMN ""{column}"" TYPE boolean
      USING (""{column}"" <> 0);
  END IF;
END $$;
");
            }
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Reverting boolean → integer is intentionally not supported.
            // Roll back by restoring from a backup or re-running InitialCreate on a fresh DB.
        }
    }
}
