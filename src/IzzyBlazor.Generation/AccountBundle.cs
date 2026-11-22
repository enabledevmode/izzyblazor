using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace IzzyBlazor.Generation;

/// <summary>
/// An account's copy of the private catalog for one release, as a zip: the files of every account item
/// (<see cref="ItemTier.Account"/>), and nothing of Pro. The account service signs it and hands it to a
/// signed-in CLI, which checks the signature, unpacks it and loads it as
/// <c>Catalog.Load(public, folder, ItemTier.Account)</c>.
/// </summary>
public static partial class AccountBundle
{
    const string Prefix = "izzy1b";

    /// <summary>
    /// Writes the bundle from the two catalogs. From the private one it takes the folder of each item whose
    /// public manifest says <c>"tier": "account"</c>, less its Pro layouts (<c>layouts/&lt;choice&gt;.*</c> for a
    /// Pro choice). Pro items and the Pro files of free items are left out. A file kept that has a condition on
    /// a Pro choice stops the export: that markup belongs in a layout file of its own.
    /// </summary>
    /// <param name="publicRoot">The public catalog, with every item's manifest.</param>
    /// <param name="privateRoot">The private catalog.</param>
    /// <returns>The zip, the same bytes for the same files.</returns>
    public static byte[] Export(string publicRoot, string privateRoot)
    {
        using var buffer = new MemoryStream();
        using (var zip = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var folder in Directory.EnumerateDirectories(privateRoot).Where(d => !Path.GetFileName(d).StartsWith('.')).Order(StringComparer.Ordinal))
            {
                var id = Path.GetFileName(folder);
                var manifestPath = Path.Combine(publicRoot, id, "izzy.json");
                // A whole Pro item (its own izzy.json), or files for an item the public catalog doesn't have.
                if (File.Exists(Path.Combine(folder, "izzy.json")) || !File.Exists(manifestPath)) continue;
                var manifest = ItemManifest.Parse(File.ReadAllText(manifestPath));
                // A free item's folder here holds its Pro files.
                if (manifest.Tier != ItemTier.Account) continue;

                var pro = manifest.Parameters.SelectMany(p => p.ProChoices).ToHashSet(StringComparer.Ordinal);
                // The folder itself, so an item with no files of its own (a starter that only fixes its base's settings) still loads.
                Entry(zip, id + "/");
                foreach (var (path, bytes) in ItemDefinition.ReadFiles(folder))
                {
                    if (path.StartsWith("layouts/", StringComparison.Ordinal) && pro.Contains(Path.GetFileNameWithoutExtension(path))) continue;
                    if (!TemplateText.IsBinary(path))
                    {
                        var text = Encoding.UTF8.GetString(bytes);
                        foreach (var choice in pro)
                            if (Conditions().Matches(text).Any(c => c.Value.Contains($"\"{choice}\"", StringComparison.Ordinal)))
                                throw new GenerationException($"{id}/{path} has a condition on the Pro choice {choice}; put that layout in its own file, layouts/{choice}, which an account's copy leaves out.");
                    }
                    using var stream = Entry(zip, $"{id}/files/{path}").Open();
                    stream.Write(bytes);
                }
            }
        }
        return buffer.ToArray();
    }

    /// <summary>The bundle's signature, as text: <c>izzy1b.&lt;key id&gt;.&lt;signature&gt;</c>.</summary>
    public static string Sign(byte[] bundle, ECDsa privateKey, string keyId) =>
        $"{Prefix}.{keyId}.{AccountKey.Base64Url(privateKey.SignData(bundle, HashAlgorithmName.SHA256))}";

    /// <summary>Whether <paramref name="signature"/> is the account service's over these bytes.</summary>
    public static bool Verify(byte[] bundle, string? signature, IReadOnlyDictionary<string, string> publicKeys)
    {
        var parts = (signature ?? "").Trim().Split('.');
        if (parts.Length != 3 || parts[0] != Prefix) return false;
        try
        {
            return AccountKey.Verify(bundle, AccountKey.FromBase64Url(parts[2]), parts[1], publicKeys);
        }
        catch (Exception e) when (e is FormatException or CryptographicException)
        {
            return false;
        }
    }

    /// <summary>
    /// Unpacks a bundle into <paramref name="folder"/>, replacing what is there. It is unpacked beside the
    /// folder first, so a command that stops halfway leaves the old copy or none, never half of one.
    /// </summary>
    public static void Unpack(byte[] bundle, string folder)
    {
        folder = Path.GetFullPath(folder);
        var staging = folder + ".new";
        if (Directory.Exists(staging)) Directory.Delete(staging, recursive: true);
        Directory.CreateDirectory(staging);
        using (var zip = new ZipArchive(new MemoryStream(bundle), ZipArchiveMode.Read))
        {
            foreach (var entry in zip.Entries)
            {
                var target = Path.GetFullPath(Path.Combine(staging, entry.FullName));
                if (!target.StartsWith(staging + Path.DirectorySeparatorChar, StringComparison.Ordinal))
                    throw new GenerationException($"The account catalog has a file outside its folder: {entry.FullName}.");
                if (entry.FullName.EndsWith('/'))
                {
                    Directory.CreateDirectory(target);
                    continue;
                }
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                entry.ExtractToFile(target, overwrite: true);
            }
        }
        if (Directory.Exists(folder)) Directory.Delete(folder, recursive: true);
        Directory.Move(staging, folder);
    }

    // One date for every entry, so the same files make the same bytes and the same signature.
    static ZipArchiveEntry Entry(ZipArchive zip, string name)
    {
        var entry = zip.CreateEntry(name, CompressionLevel.Optimal);
        entry.LastWriteTime = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        return entry;
    }

    [GeneratedRegex(@"#(?:if|elif)\b.*")]
    private static partial Regex Conditions();
}
