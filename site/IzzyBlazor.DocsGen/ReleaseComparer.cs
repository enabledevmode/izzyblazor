namespace IzzyBlazor.DocsGen;

/// <summary>
/// Compares a new metadata file with the last release's: anything the old file had keeps its
/// "added in" version, and anything new was added in this one. Also lists what's gone, for the
/// Upgrade page. Until the version is bumped for the next release it still reads as the last
/// release's, so new members get no "added in" until then.
/// </summary>
public static class ReleaseComparer
{
    public static (ApiFile File, List<string> Removed) Apply(ApiFile current, ApiFile previous)
    {
        var removed = new List<string>();
        var version = current.Version == previous.Version ? null : current.Version;
        var oldComponents = previous.Components.ToDictionary(c => c.Name);
        var oldTypes = previous.Types.ToDictionary(t => t.Name);
        var oldBases = previous.Bases.ToDictionary(b => b.Name);

        var components = current.Components.Select(c =>
        {
            if (!oldComponents.TryGetValue(c.Name, out var old))
            {
                return c with
                {
                    AddedIn = version,
                    Properties = Mark(c.Properties, null, version),
                    Events = Mark(c.Events, null, version),
                    Methods = Mark(c.Methods, null, version),
                };
            }

            removed.AddRange(Gone(c.Name, old.Properties.Concat(old.Events).Concat(old.Methods), c.Properties.Concat(c.Events).Concat(c.Methods)));
            return c with
            {
                AddedIn = old.AddedIn,
                Properties = Mark(c.Properties, old.Properties, version),
                Events = Mark(c.Events, old.Events, version),
                Methods = Mark(c.Methods, old.Methods, version),
            };
        }).ToList();

        removed.AddRange(previous.Components.Where(o => components.All(c => c.Name != o.Name)).Select(o => o.Name));

        foreach (var old in previous.Bases)
        {
            var now = current.Bases.FirstOrDefault(b => b.Name == old.Name);
            removed.AddRange(now is null ? [old.Name] : Gone(old.Name, old.Properties, now.Properties));
        }

        foreach (var old in previous.Types)
        {
            var now = current.Types.FirstOrDefault(t => t.Name == old.Name);
            removed.AddRange(now is null ? [old.Name] : Gone(old.Name, old.Members, now.Members));
        }

        var bases = current.Bases.Select(b => b with
        {
            Properties = Mark(b.Properties, oldBases.GetValueOrDefault(b.Name)?.Properties, version),
        }).ToList();

        var types = current.Types.Select(t => oldTypes.TryGetValue(t.Name, out var old)
            ? t with { AddedIn = old.AddedIn, Members = Mark(t.Members, old.Members, version) }
            : t with { AddedIn = version, Members = Mark(t.Members, null, version) }).ToList();

        return (current with { Components = components, Bases = bases, Types = types }, removed);
    }

    /// <summary>
    /// What the next release compares with: names, types and "added in", without the docs, so the
    /// file kept in the repository stays small.
    /// </summary>
    public static ApiFile Baseline(ApiFile file)
    {
        static List<ApiMember> Names(List<ApiMember> members) =>
            members.Select(m => new ApiMember { Name = m.Name, Type = m.Type, AddedIn = m.AddedIn }).ToList();

        return file with
        {
            Components = file.Components.Select(c => c with
            {
                Summary = null, Remarks = null, Examples = null, Modes = null,
                Properties = Names(c.Properties), Events = Names(c.Events), Methods = Names(c.Methods),
            }).ToList(),
            Bases = file.Bases.Select(b => b with { Summary = null, Properties = Names(b.Properties) }).ToList(),
            Types = file.Types.Select(t => t with { Summary = null, Members = Names(t.Members) }).ToList(),
        };
    }

    private static List<ApiMember> Mark(List<ApiMember> members, List<ApiMember>? old, string? version)
    {
        var before = old?.ToDictionary(Key);
        return members.Select(m =>
            before is not null && before.TryGetValue(Key(m), out var was) ? m with { AddedIn = was.AddedIn } : m with { AddedIn = version }).ToList();
    }

    private static IEnumerable<string> Gone(string owner, IEnumerable<ApiMember> before, IEnumerable<ApiMember> now)
    {
        var keys = now.Select(Key).ToHashSet();
        return before.Where(m => !keys.Contains(Key(m))).Select(m => $"{owner}.{(IsMethod(m) ? m.Type[(m.Type.IndexOf(' ') + 1)..] : m.Name)}");
    }

    // A property by its name; a method by its signature, so a changed or removed overload counts.
    private static string Key(ApiMember member) => IsMethod(member) ? member.Type : member.Name;

    private static bool IsMethod(ApiMember member) =>
        member.Type.Contains($" {member.Name}(", StringComparison.Ordinal) || member.Type.Contains($" {member.Name}<", StringComparison.Ordinal);
}
