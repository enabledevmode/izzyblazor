namespace IzzyBlazor;

/// <summary>
/// What <see cref="Table{TItem}"/> and <see cref="DataGrid{TItem}"/> share: their columns, sort,
/// filters, and the table they draw.
/// </summary>
/// <typeparam name="TItem">The type of the items.</typeparam>
public abstract partial class TableViewBase<TItem> : IDataGridExportFormats
{
    // Through the cascade the columns already get, whose type is this one's: a cascade of its own
    // around the columns would move their static forms, which Blazor tracks by where they are.
    void IDataGridExportFormats.AddExportFormat(DataGridExportFormat format) => AddExportFormat(format);

    void IDataGridExportFormats.RemoveExportFormat(DataGridExportFormat format) => RemoveExportFormat(format);

    void IDataGridExportFormats.ExportFormatChanged() => ExportFormatChanged();

    private protected virtual void AddExportFormat(DataGridExportFormat format) =>
        throw new InvalidOperationException("A DataGridExportFormat must be inside a DataGrid, among its columns.");

    private protected virtual void RemoveExportFormat(DataGridExportFormat format)
    {
    }

    private protected virtual void ExportFormatChanged()
    {
    }
}
