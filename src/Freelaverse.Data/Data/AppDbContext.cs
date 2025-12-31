using FreelaverseApi.Models;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace FreelaverseApi.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) {}

    public DbSet<User> Users => Set<User>();
    public DbSet<Service> Services => Set<Service>();
    public DbSet<ProfessionalService> ProfessionalServices => Set<ProfessionalService>();
    public DbSet<UserProfessionalAreas> UserProfessionalArea => Set<UserProfessionalAreas>();
    public DbSet<ProfessionalAreas> ProfessionalArea => Set<ProfessionalAreas>();

    public override int SaveChanges()
    {
        AplicarAuditoria();
        return base.SaveChanges();
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        AplicarAuditoria();
        return base.SaveChangesAsync(cancellationToken);
    }

    private void AplicarAuditoria()
    {
        var agora = DateTimeOffset.UtcNow;

        foreach (var entry in ChangeTracker.Entries()
            .Where(e => e.Entity is IAuditable)
            .Where(e => e.State == EntityState.Added || e.State == EntityState.Modified))
        {
            ((IAuditable)entry.Entity).UpdatedAt = agora;

            if (entry.State == EntityState.Added)
                ((IAuditable)entry.Entity).CreatedAt = agora;
            else
                entry.Property(nameof(IAuditable.CreatedAt)).IsModified = false;
        }
    }
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}