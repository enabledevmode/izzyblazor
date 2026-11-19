using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace {{RootNamespace}}.Data;

/// <summary>
/// A company's logo: the picture itself, kept apart from <see cref="Company"/> so lists of companies don't load it.
/// Program.cs serves it at <c>/company-logos/{id}</c>.
/// </summary>
public class CompanyLogo
{
    /// <summary>The largest logo taken, in bytes: what the form's upload says ("up to 2 MB").</summary>
    public const long MaxBytes = 2_000_000;

    [Key] public int CompanyId { get; set; }

    /// <summary><c>image/png</c> or <c>image/jpeg</c>.</summary>
    [Required, StringLength(40)] public string ContentType { get; set; } = "";

    public byte[] Data { get; set; } = [];

    /// <summary>
    /// Reads an uploaded file as a logo: a PNG or JPG of <see cref="MaxBytes"/> at most, told by its first bytes
    /// rather than its name. Null with a <paramref name="problem"/> to show when it's anything else.
    /// </summary>
    public static async Task<(CompanyLogo? Logo, string? Problem)> ReadAsync(IFormFile file)
    {
        if (file.Length > MaxBytes) return (null, "Choose a logo of 2 MB or less.");
        using var stream = new MemoryStream();
        await file.CopyToAsync(stream);
        var data = stream.ToArray();
        var type = data.AsSpan().StartsWith<byte>([0x89, 0x50, 0x4E, 0x47]) ? "image/png"
            : data.AsSpan().StartsWith<byte>([0xFF, 0xD8, 0xFF]) ? "image/jpeg"
            : null;
        return type is null ? (null, "Choose a PNG or JPG file.") : (new CompanyLogo { ContentType = type, Data = data }, null);
    }
}
