using Microsoft.EntityFrameworkCore;

namespace template.data;

/// <summary>
/// The one DbContext. Every provider maps the same model; only the migrations differ.
/// </summary>
public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Widget> Widgets => Set<Widget>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Widget>(widget =>
        {
            widget.HasKey(w => w.Id);

            // Unique rather than merely indexed: the name is the human-facing identity here, and
            // WidgetSeeder's replace-all pass would otherwise be free to leave two rows that read
            // identically in the UI.
            widget.HasIndex(w => w.Name).IsUnique();

            widget.Property(w => w.Name).HasMaxLength(64);
        });
    }
}
