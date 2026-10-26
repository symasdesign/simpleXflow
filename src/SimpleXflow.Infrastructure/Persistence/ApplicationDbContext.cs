using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using SimpleXflow.Application.Abstractions;
using SimpleXflow.Domain.Common;
using SimpleXflow.Domain.Projects;
using SimpleXflow.Domain.Tenants;
using SimpleXflow.Domain.Workspaces;
using SimpleXflow.Infrastructure.Identity;

namespace SimpleXflow.Infrastructure.Persistence;

public class ApplicationDbContext(
    DbContextOptions options,
    ITenantContext tenantContext)
    : IdentityDbContext<ApplicationUser>(options), IDataProtectionKeyContext
{
    public DbSet<Tenant> Tenants => Set<Tenant>();

    public DbSet<FlowProject> Projects => Set<FlowProject>();

    public DbSet<ProjectAttachment> ProjectAttachments => Set<ProjectAttachment>();

    public DbSet<ProjectVersion> ProjectVersions => Set<ProjectVersion>();

    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();

    public DbSet<Workspace> Workspaces => Set<Workspace>();
    public DbSet<WorkspaceMember> WorkspaceMembers => Set<WorkspaceMember>();
    public DbSet<WorkspaceInvitation> WorkspaceInvitations => Set<WorkspaceInvitation>();
    public DbSet<WorkspaceProject> WorkspaceProjects => Set<WorkspaceProject>();
    public DbSet<ModelFolder> ModelFolders => Set<ModelFolder>();
    public DbSet<WorkPackage> WorkPackages => Set<WorkPackage>();
    public DbSet<ModelEditingPresence> ModelEditingPresences => Set<ModelEditingPresence>();

    private readonly HashSet<Guid> authorizedModelWrites = [];
    private readonly SemaphoreSlim operationGate = new(1, 1);
    internal async Task<IDisposable> EnterOperationAsync(CancellationToken cancellationToken)
    {
        await operationGate.WaitAsync(cancellationToken);
        ChangeTracker.Clear();
        authorizedModelWrites.Clear();
        return new OperationScope(operationGate);
    }
    private sealed class OperationScope(SemaphoreSlim gate) : IDisposable
    {
        public void Dispose() => gate.Release();
    }
    // Only called by services after checking current workspace membership and role.
    internal void AuthorizeModelWrite(Guid modelId) => authorizedModelWrites.Add(modelId);

    // EF evaluates filter parameters independently, including when no tenant is available.
    private Guid? CurrentTenantId => tenantContext.IsAvailable ? tenantContext.TenantId : null;
    private string? CurrentUserId => tenantContext.UserId;

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
            entity.Property(project => project.PreviousName).HasMaxLength(240);
            entity.HasIndex(project => new { project.TenantId, project.Name }).IsUnique()
                .HasFilter("\"WorkspaceProjectId\" IS NULL");
            entity.HasIndex(project => new { project.WorkspaceProjectId, project.FolderPath, project.Name }).IsUnique()
                .HasFilter("\"WorkspaceProjectId\" IS NOT NULL");
            entity.Property(project => project.FolderPath).HasMaxLength(500).HasDefaultValue("");
            entity.Property(project => project.LastEditedBy).HasMaxLength(320);
            entity.Property(project => project.Revision).IsConcurrencyToken();
            entity.HasOne<WorkspaceProject>().WithMany().HasForeignKey(project => project.WorkspaceProjectId).OnDelete(DeleteBehavior.Restrict);
            entity.HasQueryFilter(project => !CurrentTenantId.HasValue
                || (project.WorkspaceProjectId == null && project.TenantId == CurrentTenantId)
                || WorkspaceProjects.Any(container => container.Id == project.WorkspaceProjectId
                    && WorkspaceMembers.Any(member => member.WorkspaceId == container.WorkspaceId && member.UserId == CurrentUserId)));

            entity.HasMany(project => project.Attachments)
                .WithOne(attachment => attachment.Project)
                .HasForeignKey(attachment => attachment.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(project => project.Versions)
                .WithOne()
                .HasForeignKey(version => version.FlowProjectId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.Navigation(project => project.Versions)
                .UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        builder.Entity<ProjectAttachment>(entity =>
        {
            entity.HasKey(attachment => attachment.Id);
            entity.Property(attachment => attachment.FileName).HasMaxLength(260).IsRequired();
            entity.Property(attachment => attachment.ContentType).HasMaxLength(120).IsRequired();
            entity.Property(attachment => attachment.Content).IsRequired();
            entity.HasQueryFilter(attachment => !CurrentTenantId.HasValue || Projects.Any(project => project.Id == attachment.ProjectId));
        });

        builder.Entity<ProjectVersion>(entity =>
        {
            entity.HasKey(version => version.Id);
            entity.Property(version => version.Name).HasMaxLength(240).IsRequired();
            entity.Property(version => version.BpmnXml).IsRequired();
            entity.HasIndex(version => new { version.TenantId, version.FlowProjectId, version.VersionNumber }).IsUnique();
            entity.Property(version => version.Label).HasMaxLength(240).HasDefaultValue("");
            entity.Property(version => version.CreatedBy).HasMaxLength(320);
            entity.Property(version => version.ReleasedBy).HasMaxLength(320);
            entity.HasQueryFilter(version => !CurrentTenantId.HasValue || Projects.Any(project => project.Id == version.FlowProjectId));
        });

        builder.Entity<Workspace>(entity =>
        {
            entity.Property(item => item.Name).HasMaxLength(200).IsRequired();
            entity.Property(item => item.OwnerUserId).HasMaxLength(450).IsRequired();
            entity.HasOne<Tenant>().WithMany().HasForeignKey(item => item.OrganizationId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(item => item.OwnerUserId).OnDelete(DeleteBehavior.Restrict);
        });
        builder.Entity<WorkspaceMember>(entity =>
        {
            entity.HasKey(item => new { item.WorkspaceId, item.UserId });
            entity.Property(item => item.Role).HasMaxLength(16).IsRequired();
            entity.HasOne<Workspace>().WithMany().HasForeignKey(item => item.WorkspaceId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(item => item.UserId).OnDelete(DeleteBehavior.Restrict);
        });
        builder.Entity<WorkspaceInvitation>(entity =>
        {
            entity.Property(item => item.Email).HasMaxLength(320).IsRequired();
            entity.Property(item => item.Role).HasMaxLength(16).IsRequired();
            entity.Property(item => item.TokenHash).HasMaxLength(64).IsRequired();
            entity.HasIndex(item => item.TokenHash).IsUnique();
            entity.Property(item => item.AcceptedUtc).IsConcurrencyToken();
            entity.Property(item => item.RevokedUtc).IsConcurrencyToken();
            entity.HasOne<Workspace>().WithMany().HasForeignKey(item => item.WorkspaceId).OnDelete(DeleteBehavior.Cascade);
        });
        builder.Entity<WorkspaceProject>(entity =>
        {
            entity.Property(item => item.Name).HasMaxLength(240).IsRequired();
            entity.HasIndex(item => new { item.WorkspaceId, item.Name }).IsUnique();
            entity.HasOne<Workspace>().WithMany().HasForeignKey(item => item.WorkspaceId).OnDelete(DeleteBehavior.Cascade);
        });
        builder.Entity<ModelFolder>(entity =>
        {
            entity.Property(item => item.Path).HasMaxLength(500).IsRequired();
            entity.HasIndex(item => new { item.WorkspaceProjectId, item.Path }).IsUnique();
            entity.HasOne<WorkspaceProject>().WithMany().HasForeignKey(item => item.WorkspaceProjectId).OnDelete(DeleteBehavior.Cascade);
        });
        builder.Entity<WorkPackage>(entity =>
        {
            entity.Property(item => item.Title).HasMaxLength(240).IsRequired();
            entity.Property(item => item.Description).HasMaxLength(4000);
            entity.Property(item => item.Status).HasMaxLength(24).IsRequired();
            entity.HasOne<Workspace>().WithMany().HasForeignKey(item => item.WorkspaceId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(item => item.AssigneeUserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<FlowProject>().WithMany().HasForeignKey(item => item.ModelId).OnDelete(DeleteBehavior.SetNull);
        });
        builder.Entity<ModelEditingPresence>(entity =>
        {
            entity.HasKey(item => new { item.ModelId, item.UserId });
            entity.HasOne<FlowProject>().WithMany().HasForeignKey(item => item.ModelId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(item => item.UserId).OnDelete(DeleteBehavior.Cascade);
        });
    }

    // DbContext delegates its parameterless save methods to these overloads.
    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        GuardTenantEntities();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        GuardTenantEntities();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
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

            if (entry.Entity.TenantId != tenantContext.TenantId
                || (entry.State != EntityState.Added
                    && entry.Property(entity => entity.TenantId).OriginalValue != tenantContext.TenantId))
            {
                var modelId = entry.Entity switch
                {
                    FlowProject model => model.Id,
                    ProjectVersion version => version.FlowProjectId,
                    ProjectAttachment attachment => attachment.ProjectId,
                    _ => Guid.Empty
                };
                if (!authorizedModelWrites.Contains(modelId)
                    || (entry.State != EntityState.Added && entry.Property(entity => entity.TenantId).OriginalValue != entry.Entity.TenantId))
                {
                    throw new InvalidOperationException("Tenant scoped data cannot be changed from another tenant.");
                }
            }
        }
    }
}
