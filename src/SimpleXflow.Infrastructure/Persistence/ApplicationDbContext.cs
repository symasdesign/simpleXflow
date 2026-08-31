using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using SimpleXflow.Application.Abstractions;
using SimpleXflow.Domain.Common;
using SimpleXflow.Domain.Projects;
using SimpleXflow.Domain.Tenants;
using SimpleXflow.Infrastructure.Identity;

namespace SimpleXflow.Infrastructure.Persistence;

public class ApplicationDbContext(
    DbContextOptions options,
    ITenantContext tenantContext)
    : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<Tenant> Tenants => Set<Tenant>();

    public DbSet<FlowProject> Projects => Set<FlowProject>();

    public DbSet<ProjectAttachment> ProjectAttachments => Set<ProjectAttachment>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Tenant>(entity =>
        {
            entity.HasKey(tenant => tenant.Id);
            entity.Property(tenant => tenant.Name).HasMaxLength(200).IsRequired();
            entity.Property(tenant => tenant.Slug).HasMaxLength(220).IsRequired();
            entity.HasIndex(tenant => tenant.Slug).IsUnique();
        });

        builder.Entity<ApplicationUser>(entity =>
        {
            entity.HasOne(user => user.Tenant)
                .WithMany()
                .HasForeignKey(user => user.TenantId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(user => user.TenantId);
        });

        builder.Entity<FlowProject>(entity =>
        {
            entity.HasKey(project => project.Id);
            entity.Property(project => project.Name).HasMaxLength(240).IsRequired();
            entity.Property(project => project.BpmnXml).IsRequired();
            entity.HasIndex(project => new { project.TenantId, project.Name }).IsUnique();
            entity.HasQueryFilter(project => !tenantContext.IsAvailable || project.TenantId == tenantContext.TenantId);

            entity.HasMany(project => project.Attachments)
                .WithOne(attachment => attachment.Project)
                .HasForeignKey(attachment => attachment.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<ProjectAttachment>(entity =>
        {
            entity.HasKey(attachment => attachment.Id);
            entity.Property(attachment => attachment.FileName).HasMaxLength(260).IsRequired();
            entity.Property(attachment => attachment.ContentType).HasMaxLength(120).IsRequired();
            entity.Property(attachment => attachment.Content).IsRequired();
            entity.HasQueryFilter(attachment => !tenantContext.IsAvailable || attachment.TenantId == tenantContext.TenantId);
        });
    }

    public override int SaveChanges()
    {
        GuardTenantEntities();
        return base.SaveChanges();
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        GuardTenantEntities();
        return base.SaveChangesAsync(cancellationToken);
    }

    private void GuardTenantEntities()
    {
        foreach (var entry in ChangeTracker.Entries<ITenantEntity>())
        {
            if (entry.State is EntityState.Detached or EntityState.Unchanged || !tenantContext.IsAvailable)
            {
                continue;
            }

            if (entry.State == EntityState.Added && entry.Entity.TenantId == Guid.Empty)
            {
                entry.Entity.TenantId = tenantContext.TenantId;
            }

            if (entry.Entity.TenantId != tenantContext.TenantId)
            {
                throw new InvalidOperationException("Tenant scoped data cannot be changed from another tenant.");
            }
        }
    }
}
