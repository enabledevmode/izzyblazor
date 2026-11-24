namespace IzzyBlazor.Cli;

/// <summary>A wrong command line: the message says what to type instead.</summary>
sealed class UsageException(string message) : Exception(message);

/// <summary>
/// A command line split into words before the first option (the command and item) and options. An option takes
/// the next word as its value unless that word starts with "-" and isn't a number (<c>--min -5</c>); the CLI's
/// own switches never take one.
/// </summary>
sealed class Args
{
    static readonly HashSet<string> Switches = ["json", "dry-run", "diff", "overwrite", "help", "version", "no-build", "no-browser", "tag", "stdin", "all", "run", "pro", "yes"];
    static readonly Dictionary<string, string> Short = new() { ["n"] = "name", ["o"] = "output", ["h"] = "help" };

    public List<string> Words { get; } = [];

    /// <summary>Options by name without dashes, in the order given; a switch or a bare option has a null value.</summary>
    public List<(string Name, string? Value)> Options { get; } = [];

    public static Args Parse(string[] args)
    {
        var parsed = new Args();
        for (var i = 0; i < args.Length; i++)
        {
            var word = args[i];
            string? name = word.StartsWith("--", StringComparison.Ordinal) ? word[2..]
                : word.StartsWith('-') && word.Length > 1 ? (Short.TryGetValue(word[1..], out var full) ? full : throw new UsageException($"Unknown option {word}."))
                : null;
            if (name is null)
            {
                if (parsed.Options.Count > 0) throw new UsageException($"\"{word}\" follows an option that takes no value. Put the item first: izzy add block hero --layout Split.");
                parsed.Words.Add(word);
                continue;
            }
            string? value = null;
            var equals = name.IndexOf('=');
            if (equals >= 0) (name, value) = (name[..equals], name[(equals + 1)..]);
            else if (!Switches.Contains(name) && i + 1 < args.Length && (!args[i + 1].StartsWith('-') || Number(args[i + 1]))) value = args[++i];
            parsed.Options.Add((name.ToLowerInvariant(), value));
        }
        return parsed;
    }

    // -5 and -.5 are values, not options.
    static bool Number(string word) => word.Length > 1 && (char.IsAsciiDigit(word[1]) || (word[1] == '.' && word.Length > 2 && char.IsAsciiDigit(word[2])));

    public bool Has(string name) => Options.Any(o => o.Name == name);

    /// <summary>The value of a CLI option (not a setting), removing it so what's left are settings.</summary>
    public string? Take(string name)
    {
        var index = Options.FindIndex(o => o.Name == name);
        if (index < 0) return null;
        var value = Options[index].Value ?? throw new UsageException($"--{name} needs a value.");
        Options.RemoveAt(index);
        return value;
    }

    /// <summary>Whether a switch was given, removing it; <c>--overwrite=false</c> is not given.</summary>
    public bool Switch(string name)
    {
        var given = Options.Where(o => o.Name == name).ToList();
        Options.RemoveAll(o => o.Name == name);
        return given.Count > 0 && !given.Any(o => string.Equals(o.Value, "false", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>The names the CLI takes for itself, which no item's setting may be given under.</summary>
    public static bool Reserved(string name) =>
        Switches.Contains(name) || Short.ContainsKey(name) || name is "project" or "at" or "kind" or "example" or "unset";
}
