using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Migrations.Internal;

namespace SecurityScanDashboard.Data
{
    // Custom History Repository to use "EFMigrationsHistory" instead of "__EFMigrationsHistory"
    // Because the school doesn't accept double underscores
#pragma warning disable EF1001 // Internal EF Core API usage
    public class CustomHistoryRepository : NpgsqlHistoryRepository
    {
        public CustomHistoryRepository(HistoryRepositoryDependencies dependencies)
            : base(dependencies)
        {
        }

        protected override string TableName => "EFMigrationsHistory";
        
        protected override string TableSchema => "belek_appsec";

        protected override void ConfigureTable(EntityTypeBuilder<HistoryRow> history)
        {
            base.ConfigureTable(history);
            history.ToTable("EFMigrationsHistory", "belek_appsec");
        }
    }
#pragma warning restore EF1001
}
