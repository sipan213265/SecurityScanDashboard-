using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SecurityScanDashboard.Migrations
{
    /// <inheritdoc />
    public partial class AddProjectsAndVulnValidation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Idempotent: safe even if SQL was already applied manually in Neon

            // 1. Rename CreatedAt -> created_at on Repositories if still PascalCase
            migrationBuilder.Sql(
                "DO $rename$ BEGIN " +
                "IF EXISTS (SELECT 1 FROM information_schema.columns " +
                "  WHERE table_schema='belek_appsec' AND table_name='Repositories' AND column_name='CreatedAt') " +
                "THEN ALTER TABLE belek_appsec.\"Repositories\" RENAME COLUMN \"CreatedAt\" TO created_at; " +
                "END IF; END $rename$;"
            );

            // 2. Vulnerability new columns
            migrationBuilder.Sql(
                "ALTER TABLE belek_appsec.\"Vulnerabilities\" " +
                "ADD COLUMN IF NOT EXISTS \"MatchedAt\" character varying(1000), " +
                "ADD COLUMN IF NOT EXISTS \"Remediation\" text, " +
                "ADD COLUMN IF NOT EXISTS \"ValidatedAt\" timestamp with time zone, " +
                "ADD COLUMN IF NOT EXISTS \"ValidatedBy\" integer, " +
                "ADD COLUMN IF NOT EXISTS \"ValidationNotes\" character varying(1000), " +
                "ADD COLUMN IF NOT EXISTS \"ValidationStatus\" text NOT NULL DEFAULT 'Unreviewed';"
            );

            // 3. Repository ProjectId column
            migrationBuilder.Sql(
                "ALTER TABLE belek_appsec.\"Repositories\" " +
                "ADD COLUMN IF NOT EXISTS \"ProjectId\" integer;"
            );

            // 4. Projects table
            migrationBuilder.Sql(
                "CREATE TABLE IF NOT EXISTS belek_appsec.\"Projects\" (" +
                "  \"Id\" serial PRIMARY KEY, " +
                "  \"Name\" character varying(200) NOT NULL, " +
                "  \"Description\" character varying(1000), " +
                "  \"OwnerId\" integer NOT NULL, " +
                "  created_at timestamp with time zone NOT NULL DEFAULT NOW(), " +
                "  updated_at timestamp with time zone, " +
                "  CONSTRAINT \"FK_Projects_users_OwnerId\" FOREIGN KEY (\"OwnerId\") " +
                "    REFERENCES public.users(id) ON DELETE RESTRICT" +
                ");"
            );

            // 5. Indexes
            migrationBuilder.Sql(
                "CREATE INDEX IF NOT EXISTS \"IX_Projects_OwnerId\" " +
                "  ON belek_appsec.\"Projects\"(\"OwnerId\");"
            );
            migrationBuilder.Sql(
                "CREATE INDEX IF NOT EXISTS \"IX_Repositories_ProjectId\" " +
                "  ON belek_appsec.\"Repositories\"(\"ProjectId\");"
            );

            // 6. FK Repositories -> Projects
            migrationBuilder.Sql(
                "DO $fk$ BEGIN " +
                "IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname='FK_Repositories_Projects_ProjectId') " +
                "THEN ALTER TABLE belek_appsec.\"Repositories\" " +
                "  ADD CONSTRAINT \"FK_Repositories_Projects_ProjectId\" " +
                "  FOREIGN KEY (\"ProjectId\") REFERENCES belek_appsec.\"Projects\"(\"Id\") ON DELETE SET NULL; " +
                "END IF; END $fk$;"
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "ALTER TABLE belek_appsec.\"Repositories\" " +
                "  DROP CONSTRAINT IF EXISTS \"FK_Repositories_Projects_ProjectId\";"
            );
            migrationBuilder.Sql(
                "DROP INDEX IF EXISTS belek_appsec.\"IX_Repositories_ProjectId\";"
            );
            migrationBuilder.Sql(
                "DROP INDEX IF EXISTS belek_appsec.\"IX_Projects_OwnerId\";"
            );
            migrationBuilder.Sql(
                "DROP TABLE IF EXISTS belek_appsec.\"Projects\";"
            );
            migrationBuilder.Sql(
                "ALTER TABLE belek_appsec.\"Repositories\" DROP COLUMN IF EXISTS \"ProjectId\";"
            );
            migrationBuilder.Sql(
                "ALTER TABLE belek_appsec.\"Vulnerabilities\" " +
                "DROP COLUMN IF EXISTS \"MatchedAt\", " +
                "DROP COLUMN IF EXISTS \"Remediation\", " +
                "DROP COLUMN IF EXISTS \"ValidatedAt\", " +
                "DROP COLUMN IF EXISTS \"ValidatedBy\", " +
                "DROP COLUMN IF EXISTS \"ValidationNotes\", " +
                "DROP COLUMN IF EXISTS \"ValidationStatus\";"
            );
        }
    }
}
