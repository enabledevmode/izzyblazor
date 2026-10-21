namespace IzzyBlazor.DocsGen;

/// <summary>
/// Compares a new metadata file with the last release's: anything the old file had keeps its
/// "added in" version, and anything new was added in this one. Also lists what's gone, for the
/// Upgrade page.
/// </summary>
public static class ReleaseComparer
{
    public static (ApiFile File, List<string> Removed) Apply(ApiFile current, ApiFile previous)
    {
        var removed = new List<string>();
        var oldComponents = previous.Components.ToDictionary(c => c.Name);
        var oldTypes = previous.Types.ToDictionary(t => t.Name);
        var oldBases = previous.Bases.ToDictionary(b => b.Name);

        var components = current.Components.Select(c =>
        {
            if (!oldComponents.TryGetValue(c.Name, out var old))
            {
                return c with
                {
                    AddedIn = current.Version,
                    Properties = Mark(c.Properties, null, current.Version),
                    Events = Mark(c.Events, null, current.Version),
                    Methods = Mark(c.Methods, null, current.Version),
                };
            }

            removed.AddRange(Gone(c.Name, old.Properties.Concat(old.Events).Concat(old.Methods), c.Properties.Concat(c.Events).Concat(c.Methods)));
            return c with
            {
                AddedIn = old.AddedIn,
                Properties = Mark(c.Properties, old.Properties, current.Version),
                Events = Mark(c.Events, old.Events, current.Version),
                Methods = Mark(c.Methods, old.Methods, current.Version),
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
            Properties = Mark(b.Properties, oldBases.GetValueOrDefault(b.Name)?.Properties, current.Version),
        }).ToList();

        var types = current.Types.Select(t => oldTypes.TryGetValue(t.Name, out var old)
            ? t with { AddedIn = old.AddedIn, Members = Mark(t.Members, old.Members, current.Version) }
            : t with { AddedIn = current.Version }).ToList();

        return (current with { Components = components, Bases = bases, Types = types }, removed);
    }

    private static List<ApiMember> Mark(List<ApiMember> members, List<ApiMember>? old, string version)
    {
        var before = old?.ToDictionary(m => m.Name + "|" + (m.Type.Contains('(') ? m.Type : ""));
        return members.Select(m =>
        {
            var key = m.Name + "|" + (m.Type.Contains('(') ? m.Type : "");
            return before is not null && before.TryGetValue(key, out var was) ? m with { AddedIn = was.AddedIn } : m with { AddedIn = version };
        }).ToList();
    }

    private static IEnumerable<string> Gone(string owner, IEnumerable<ApiMember> before, IEnumerable<ApiMember> now)
    {
        var names = now.Select(m => m.Name).ToHashSet();
        return before.Where(m => !names.Contains(m.Name)).Select(m => $"{owner}.{m.Name}");
    }
}
