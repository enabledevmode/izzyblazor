using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace {{RootNamespace}}.Data;

/// <summary>
/// A file attached to a deal, such as a proposal: kept in the database, so the app needs no file storage.
/// Program.cs serves it at <c>/deal-attachments/{id}</c>. Lists read its name and size, never <see cref="Data"/>.
/// </summary>
public class DealAttachment
{
    /// <summary>The largest file taken, in bytes: what the upload says ("up to 10 MB").</summary>
    public const long MaxBytes = 10_000_000;

    /// <summary>How many files one upload takes. Program.cs lets a request be large enough to hold them.</summary>
    public const int MaxFiles = 10;

    public int Id { get; set; }

    public int DealId { get; set; }
    public Deal? Deal { get; set; }

    [Required, StringLength(200)] public string FileName { get; set; } = "";

    /// <summary>The type the browser gave it. It's only ever sent back as a download.</summary>
    [Required, StringLength(100)] public string ContentType { get; set; } = "";

    /// <summary>Its size in bytes.</summary>
    public long Size { get; set; }

    public byte[] Data { get; set; } = [];

    /// <summary>When it was attached, in UTC.</summary>
    public DateTime Added { get; set; }

    public string? AddedById { get; set; }
    public ApplicationUser? AddedBy { get; set; }

    /// <summary>
    /// Reads an uploaded file as an attachment: any file of <see cref="MaxBytes"/> at most. Null with a
    /// <paramref name="problem"/> to show when it's empty or too large.
    /// </summary>
    public static async Task<(DealAttachment? File, string? Problem)> ReadAsync(IFormFile? file)
    {
        if (file is not { Length: > 0 }) return (null, "Choose a file.");
        if (file.Length > MaxBytes) return (null, "Choose a file of 10 MB or less.");
        using var stream = new MemoryStream();
        await file.CopyToAsync(stream);
        // The name without any folder the browser sent with it.
        var name = Path.GetFileName(file.FileName.Replace('\\', '/')).Trim();
        if (name.Length == 0) name = "File";
        return (new DealAttachment
        {
            FileName = name.Length <= 200 ? name : name[..200],
            ContentType = file.ContentType is { Length: > 0 and <= 100 } type ? type : "application/octet-stream",
            Size = file.Length,
            Data = stream.ToArray(),
        }, null);
    }

    /// <summary>A size as people read it: "248 KB", "1.2 MB".</summary>
    public static string SizeText(long bytes) => bytes switch
    {
        < 1000 => $"{bytes} bytes",
        < 1_000_000 => $"{bytes / 1000.0:0} KB",
        _ => $"{bytes / 1_000_000.0:0.#} MB",
    };
}
