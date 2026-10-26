namespace SimpleXflow.Application.Projects;

public interface IFlowProjectService
{
    Task<IReadOnlyList<ProjectSummary>> GetProjectsAsync(CancellationToken cancellationToken = default);

    Task<ProjectDetail?> GetProjectAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Guid> CreateProjectAsync(CreateProjectRequest request, CancellationToken cancellationToken = default);

    Task<long> UpdateProjectAsync(Guid id, UpdateProjectRequest request, CancellationToken cancellationToken = default);

    Task UndoProjectAsync(Guid id, CancellationToken cancellationToken = default);

    Task DeleteProjectAsync(Guid id, long expectedRevision, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ModelVersionSummary>> GetVersionsAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Guid> CreateVersionAsync(Guid id, string label, DateTimeOffset? plannedReleaseUtc = null, CancellationToken cancellationToken = default);
    Task ReleaseVersionAsync(Guid id, Guid versionId, CancellationToken cancellationToken = default);
    Task<long> RestoreVersionAsync(Guid id, Guid versionId, long expectedRevision, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ModelPresence>> HeartbeatAsync(Guid id, CancellationToken cancellationToken = default);
    Task LeaveModelAsync(Guid id, CancellationToken cancellationToken = default);
}
