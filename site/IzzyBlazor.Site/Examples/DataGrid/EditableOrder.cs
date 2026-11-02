using System.ComponentModel.DataAnnotations;

namespace IzzyBlazor.Site.Examples.DataGrid;

// On a static page a row needs a parameterless constructor and settable properties, for the post to
// fill. The attributes make the form: [Required] marks and checks a field, and MultilineText makes
// it a text area.
public sealed class EditableOrder
{
    public int Number { get; set; }

    [Required, StringLength(60)]
    public string? Customer { get; set; }

    public DateOnly Date { get; set; }

    public OrderStatus Status { get; set; }

    [Range(0, 1_000_000)]
    public decimal Total { get; set; }

    [StringLength(200), DataType(DataType.MultilineText)]
    public string? Notes { get; set; }
}

public enum OrderStatus { Paid, Pending, Refunded, Failed }
