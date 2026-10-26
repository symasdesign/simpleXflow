using Microsoft.EntityFrameworkCore;
using SimpleXflow.Application.Abstractions;
using SimpleXflow.Infrastructure.Identity;
using SimpleXflow.Infrastructure.Persistence;

namespace SimpleXflow.Infrastructure.Workspaces;

internal sealed class WorkspaceAccess(ApplicationDbContext db, ITenantContext tenant)
{
    public async Task<ApplicationUser> UserAsync(CancellationToken cancellationToken)
    {
        if (!tenant.IsAvailable || string.IsNullOrWhiteSpace(tenant.UserId))
            throw new InvalidOperationException("Please sign in to manage workspaces.");
        return await db.Users.AsNoTracking().SingleOrDefaultAsync(user => user.Id == tenant.UserId && user.TenantId == tenant.TenantId, cancellationToken)
            ?? throw new InvalidOperationException("Your account is no longer available. Please sign in again.");
    }

    public async Task<string> RoleAsync(Guid workspaceId, CancellationToken cancellationToken)
    {
        await UserAsync(cancellationToken);
        return await db.WorkspaceMembers.AsNoTracking().Where(member => member.WorkspaceId == workspaceId && member.UserId == tenant.UserId)
            .Select(member => member.Role).SingleOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException("This workspace is not available to your account.");
    }

    public static bool CanManage(string role) => role is "Owner" or "Admin";
    public static bool CanEdit(string role) => role is "Owner" or "Admin" or "Editor";
    public static void RequireManage(string role)
    {
        if (!CanManage(role)) throw new InvalidOperationException("Only a workspace owner or administrator can do this.");
    }
    public static void RequireEdit(string role)
    {
        if (!CanEdit(role)) throw new InvalidOperationException("This workspace is read-only for your account.");
    }
    public static string Name(string value, int maximum = 240)
    {
        var name = value.Trim();
        if (name.Length is < 1 || name.Length > maximum) throw new ArgumentException($"Use a name between 1 and {maximum} characters.");
        return name;
    }
    public static string FolderPath(string value)
    {
        var parts = value.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var path = string.Join('/', parts);
        if (path.Length > 500 || parts.Any(part => part is "." or ".." || part.Contains(':') || part.Any(char.IsControl)))
            throw new ArgumentException("Use a relative folder path without '..', drive names or control characters.");
        return path;
    }
}
