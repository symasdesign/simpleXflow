namespace SimpleXflow.Application.Projects;

public interface IFlowProjectService
{
    Task<IReadOnlyList<ProjectSummary>> GetProjectsAsync(CancellationToken cancellationToken = default);

    Task<ProjectDetail?> GetProjectAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Guid> CreateProjectAsync(CreateProjectRequest request, CancellationToken cancellationToken = default);

    Task UpdateProjectAsync(Guid id, UpdateProjectRequest request, CancellationToken cancellationToken = default);

    Task UndoProjectAsync(Guid id, CancellationToken cancellationToken = default);
}
