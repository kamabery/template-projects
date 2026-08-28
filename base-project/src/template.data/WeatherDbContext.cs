using Microsoft.EntityFrameworkCore;

namespace template.data;

/// <summary>
/// The one DbContext. Both providers map the same model; only the migrations differ.
/// </summary>
public class WeatherDbContext(DbContextOptions<WeatherDbContext> options) : DbContext(options)
{
    public DbSet<WeatherForecast> Forecasts => Set<WeatherForecast>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<WeatherForecast>(forecast =>
        {
            forecast.HasKey(f => f.Id);

            // Unique rather than merely indexed. The React app's TanStack DB collection keys rows
            // by date (src/ui/react/src/db/collections.ts, getKey: forecast => forecast.date), so
            // two rows sharing a date would collapse into one in that client without any error
            // being raised. Enforce it where it can actually be enforced.
            forecast.HasIndex(f => f.Date).IsUnique();

            forecast.Property(f => f.Summary).HasMaxLength(64);
        });
    }
}
