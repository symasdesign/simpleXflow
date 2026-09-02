using Microsoft.EntityFrameworkCore;
using SimpleXflow.Application.Abstractions;
using SimpleXflow.Application.Projects;
using SimpleXflow.Domain.Projects;
using SimpleXflow.Infrastructure.Persistence;

namespace SimpleXflow.Infrastructure.Projects;

public sealed class FlowProjectService(
    ApplicationDbContext dbContext,
    ITenantContext tenantContext)
    : IFlowProjectService
{
    public async Task<IReadOnlyList<ProjectSummary>> GetProjectsAsync(CancellationToken cancellationToken = default)
    {
        EnsureTenant();

        var projects = await dbContext.Projects
            .Select(project => new ProjectSummary(project.Id, project.Name, project.UpdatedUtc))
            .ToListAsync(cancellationToken);

        return projects
            .OrderByDescending(project => project.UpdatedUtc)
            .ToList();
    }

    public async Task<ProjectDetail?> GetProjectAsync(Guid id, CancellationToken cancellationToken = default)
    {
        EnsureTenant();

        return await dbContext.Projects
            .Where(project => project.Id == id)
            .Select(project => new ProjectDetail(
                project.Id,
                project.Name,
                project.BpmnXml,
                project.LogicXml,
                dbContext.ProjectVersions.Any(version => version.FlowProjectId == project.Id)
                    || (project.PreviousBpmnXml != null && project.PreviousBpmnXml != ""),
                project.CreatedUtc,
                project.UpdatedUtc))
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<Guid> CreateProjectAsync(CreateProjectRequest request, CancellationToken cancellationToken = default)
    {
        EnsureTenant();

        var projectName = await GetAvailableProjectNameAsync(request.Name, cancellationToken);
        var project = new FlowProject(tenantContext.TenantId, projectName, request.BpmnXml);
        project.UpdateModel(request.BpmnXml, request.LogicXml);
        dbContext.Projects.Add(project);
        await dbContext.SaveChangesAsync(cancellationToken);
        return project.Id;
    }

    public async Task UpdateProjectAsync(Guid id, UpdateProjectRequest request, CancellationToken cancellationToken = default)
    {
        EnsureTenant();

        var project = await dbContext.Projects
            .SingleOrDefaultAsync(project => project.Id == id, cancellationToken)
            ?? throw new InvalidOperationException("The requested project does not exist in this tenant.");

        project.UpdateProject(request.Name, request.BpmnXml, request.LogicXml);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UndoProjectAsync(Guid id, CancellationToken cancellationToken = default)
    {
        EnsureTenant();

        var project = await dbContext.Projects
            .Include(project => project.Versions)
            .SingleOrDefaultAsync(project => project.Id == id, cancellationToken)
            ?? throw new InvalidOperationException("The requested project does not exist in this tenant.");

        var existingVersions = project.Versions.ToList();
        project.UndoLastChange();
        RemoveDroppedVersions(project, existingVersions);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private void EnsureTenant()
    {
        if (!tenantContext.IsAvailable)
        {
            throw new InvalidOperationException("A tenant is required for simpleXflow project data.");
        }
    }

    private async Task<string> GetAvailableProjectNameAsync(string requestedName, CancellationToken cancellationToken)
    {
        var baseName = requestedName.Trim();
        var candidate = baseName;
        var suffix = 1;

        while (await dbContext.Projects.AnyAsync(project => project.Name == candidate, cancellationToken))
        {
            candidate = $"{baseName} ({suffix})";
            suffix++;
        }

        return candidate;
    }

    private void RemoveDroppedVersions(FlowProject project, IReadOnlyCollection<ProjectVersion> existingVersions)
    {
        var remainingVersionIds = project.Versions.Select(version => version.Id).ToHashSet();
        var droppedVersions = existingVersions.Where(version => !remainingVersionIds.Contains(version.Id));
        dbContext.ProjectVersions.RemoveRange(droppedVersions);
    }
}
