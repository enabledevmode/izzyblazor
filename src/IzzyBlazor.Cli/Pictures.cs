using System.Text.Json.Nodes;
using IzzyBlazor.Generation;

namespace IzzyBlazor.Cli;

/// <summary>
/// Where the docs site has an item's page and pictures, for a front end (an editor's panel, a coding agent) to
/// show: izzy holds no pictures itself. A block has one picture, a starter or kit a card picture and its
/// screens, a component one of its first example; each comes in the light
/// and the dark scheme.
/// </summary>
static class Pictures
{
    const string Site = "https://izzyblazor.com/";

    /// <summary>The item's page on the docs site; null for an item without one.</summary>
    public static JsonNode? Docs(ItemManifest m) => m.Kind switch
    {
        ItemKind.Block => Site + "blocks/" + m.Id,
        ItemKind.Starter or ItemKind.Kit => Site + "templates/" + m.Id,
        _ => null,
    };

    /// <summary>The picture for a list or a card, as <c>{ light, dark }</c>; null for an item without one.</summary>
    public static JsonNode? Card(ItemManifest m) => Folder(m) is { } folder
        ? new JsonObject { ["light"] = $"{Site}img/{folder}/{m.Id}.webp", ["dark"] = $"{Site}img/{folder}/{m.Id}-dark.webp" }
        : null;

    /// <summary>A starter's or kit's screens, or the pages crud makes, in order, each with its full picture and a thumbnail in both schemes.</summary>
    public static JsonArray Screens(ItemManifest m)
    {
        var at = $"{Site}img/templates/{m.Id}/";
        return new JsonArray(m.Screens.Select(s => (JsonNode)new JsonObject
        {
            ["caption"] = s.Caption, ["alt"] = s.Alt, ["phone"] = s.Phone,
            ["light"] = $"{at}{s.File}.webp", ["dark"] = $"{at}{s.File}-dark.webp",
            ["thumbnail"] = $"{at}{s.File}-thumb.webp", ["thumbnailDark"] = $"{at}{s.File}-dark-thumb.webp",
        }).ToArray());
    }

    /// <summary>
    /// A component's picture: its docs page's first example, 720px wide; null for a part, which has no page.
    /// Every component the site lists has one (ComponentPictureTests).
    /// </summary>
    public static JsonObject? Component(JsonObject c) => (string?)c["id"] is { } id
        ? new() { ["light"] = $"{Site}img/components/{id}.webp", ["dark"] = $"{Site}img/components/{id}-dark.webp" }
        : null;

    static string? Folder(ItemManifest m) => m.Kind switch
    {
        ItemKind.Block => "blocks",
        ItemKind.Starter or ItemKind.Kit => "templates",
        _ => null,
    };
}
