using SimpleXflow.Domain.Projects;

namespace SimpleXflow.Domain.Tenants;

public sealed class Tenant
{
    private readonly List<FlowProject> _projects = [];

    private Tenant()
    {
    }

    private Tenant(string name)
    {
        Id = Guid.NewGuid();
        Name = NormalizeName(name);
        Slug = CreateSlug(Name);
        CreatedUtc = DateTimeOffset.UtcNow;
    }

    public Guid Id { get; private set; }

    public string Name { get; private set; } = "";

    public string Slug { get; private set; } = "";

    public DateTimeOffset CreatedUtc { get; private set; }

    public IReadOnlyCollection<FlowProject> Projects => _projects;

    public static Tenant Create(string name) => new(name);

    public static string CreateSlug(string name) => CreateSlugCore(NormalizeName(name));

    private static string NormalizeName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Tenant name is required.", nameof(name));
        }

        return name.Trim();
    }

    private static string CreateSlugCore(string value)
    {
        var normalized = value.Trim().ToLowerInvariant();
        var chars = normalized.Select(c => char.IsLetterOrDigit(c) ? c : '-').ToArray();
        var slug = new string(chars);

        while (slug.Contains("--", StringComparison.Ordinal))
        {
            slug = slug.Replace("--", "-", StringComparison.Ordinal);
        }

        return slug.Trim('-');
    }
}
