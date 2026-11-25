using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace IzzyBlazor.DocsGen;

/// <summary>
/// Api/changes.json: what each release removed or renamed, which izzy update lists and izzy upgrade looks
/// for in an app. An entry names a component, a base or a type ("of", "name") and maybe one member of it;
/// "to" is the new name when the change is a plain rename, and "note" says what to do otherwise. The
/// entries for a release are added here when it is cut; "to" and "note" are written by hand, and kept.
/// </summary>
public static class ReleaseChanges
{
    /// <summary>Adds the removals the file doesn't have yet under the release; returns how many.</summary>
    public static int Add(string path, string version, IReadOnlyList<ApiChange> removed)
    {
        var root = File.Exists(path) ? JsonNode.Parse(File.ReadAllText(path))!.AsObject() : new JsonObject { ["format"] = 1, ["releases"] = new JsonArray() };
        var releases = root["releases"]!.AsArray();
        var release = releases.Select(r => r!.AsObject()).FirstOrDefault(r => (string?)r["version"] == version);
        if (release is null)
        {
            if (removed.Count == 0) return 0;
            releases.Add(release = new JsonObject { ["version"] = version, ["changes"] = new JsonArray() });
        }
        var changes = release["changes"]!.AsArray();
        var known = changes.Select(c => ((string?)c!["of"], (string?)c["name"], (string?)c["member"])).ToHashSet();
        var added = 0;
        foreach (var change in removed.Where(c => known.Add((c.Of, c.Name, c.Member))))
        {
            var entry = new JsonObject { ["of"] = change.Of, ["name"] = change.Name };
            if (change.Member is not null) entry["member"] = change.Member;
            changes.Add(entry);
            added++;
        }
        if (added > 0) File.WriteAllText(path, Write(root));
        return added;
    }

    // One change a line, so a release's list reads as a list and a diff shows one entry.
    static string Write(JsonObject root)
    {
        var relaxed = new JsonSerializerOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
        var text = new StringBuilder("{\n  \"format\": ").Append((int?)root["format"] ?? 1).Append(",\n  \"releases\": [\n");
        var releases = root["releases"]!.AsArray();
        for (var r = 0; r < releases.Count; r++)
        {
            var changes = releases[r]!["changes"]!.AsArray();
            text.Append("    {\n      \"version\": ").Append(releases[r]!["version"]!.ToJsonString(relaxed)).Append(",\n      \"changes\": [\n");
            for (var c = 0; c < changes.Count; c++)
            {
                var fields = changes[c]!.AsObject().Select(f => $"{JsonSerializer.Serialize(f.Key)}: {f.Value!.ToJsonString(relaxed)}");
                text.Append("        {").Append(string.Join(", ", fields)).Append('}').Append(c < changes.Count - 1 ? ",\n" : "\n");
            }
            text.Append("      ]\n    }").Append(r < releases.Count - 1 ? ",\n" : "\n");
        }
        return text.Append("  ]\n}\n").ToString();
    }
}
