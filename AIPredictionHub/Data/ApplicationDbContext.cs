using AIPredictionHub.Models;
using Microsoft.EntityFrameworkCore;

namespace AIPredictionHub.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) //dependency injection
            : base(options)
        {
        }

        public DbSet<User> Users { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
        }

        public override int SaveChanges()
        {
            AddAuditInfo();
            return base.SaveChanges();
        }

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            AddAuditInfo();
            return base.SaveChangesAsync(cancellationToken);
        }

        private void AddAuditInfo()
        {
            var entries = ChangeTracker.Entries()
                .Where(e => e.Entity is User && (e.State == EntityState.Added || e.State == EntityState.Modified));

            foreach (var entry in entries)
            {
                var user = (User)entry.Entity;
                if (entry.State == EntityState.Added)
                {
                    user.CreatedDate = DateTime.UtcNow;
                }
                else
                {
                    user.UpdatedDate = DateTime.UtcNow;
                }
            }
        }
    }
}
