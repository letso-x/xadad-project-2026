namespace MzukuluQMS.Mobile.Core;

public sealed record ProjectChoice(long Id, string Name, string? Number)
{
    public string DisplayName => string.IsNullOrWhiteSpace(Number) ? Name : $"{Name} · {Number}";
}

// Selection follows the stable server ID, never a list index or a non-unique name.
public sealed class ProjectSelection(long? initialId = null)
{
    private long? selectedId = initialId;
    public IReadOnlyList<ProjectChoice> Projects { get; private set; } = [];
    public ProjectChoice? Selected => Projects.FirstOrDefault(p => p.Id == selectedId);

    public void Refresh(IEnumerable<ProjectChoice> projects)
    {
        Projects = projects.ToArray();
        if (!Projects.Any(p => p.Id == selectedId)) selectedId = null;
    }

    public void Select(long id)
    {
        if (!Projects.Any(p => p.Id == id))
            throw new ArgumentException("Choose a project from the current accessible project list.");
        selectedId = id;
    }
}
