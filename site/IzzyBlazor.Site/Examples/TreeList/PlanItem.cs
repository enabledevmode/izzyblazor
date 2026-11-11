using System.ComponentModel.DataAnnotations;

namespace IzzyBlazor.Site.Examples.TreeList;

/// <summary>A phase or task of a plan, for the TreeList editing example: settable, as a posted form fills it.</summary>
public sealed class PlanItem
{
    public int Id { get; set; }

    public int? ParentId { get; set; }

    [Required, StringLength(60)]
    public string Name { get; set; } = "";

    public string Owner { get; set; } = "";

    [Range(0, 400)]
    public decimal Hours { get; set; }
}
