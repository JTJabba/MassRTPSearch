namespace MassRTPSearch;

using Microsoft.EntityFrameworkCore;

public class DatabaseContext : DbContext
{
    public DatabaseContext(DbContextOptions<DatabaseContext> options) : base(options)
    {
    }

    public DbSet<SearchResultsModel> SearchResults { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SearchResultsModel>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ReportedMinRtp).HasPrecision(7, 4);
            entity.Property(e => e.ReportedMaxRtp).HasPrecision(7, 4);
        });
    }

    public static DatabaseContext CreateDbContext()
    {
        var dbOptions = new DbContextOptionsBuilder<DatabaseContext>()
            .UseSqlite("Data Source=rtp_cache.db")
            .Options;
        return new DatabaseContext(dbOptions);
    }
}

public class SearchResultsModel
{
    public int Id { get; set; }
    public required string GameTitle { get; set; }
    public required DateTime SearchDate { get; set; }
    public required string PerplexityModel { get; set; }
    public required decimal ReportedMinRtp { get; set; }
    public required decimal ReportedMaxRtp { get; set; }
}
