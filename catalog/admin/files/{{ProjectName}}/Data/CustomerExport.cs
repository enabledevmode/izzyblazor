namespace {{RootNamespace}}.Data;

/// <summary>
/// The customers as an Excel file, for the grid's Export, from izzy add crud: the rows the grid shows,
/// with its search, sort and columns, which it adds to the address.
/// </summary>
public static class CustomerExport
{
    public static IEndpointConventionBuilder Map(IEndpointRouteBuilder app)
    {
        var endpoint = app.MapGet("/customers/export.xlsx", (HttpContext context, ICustomerStore store) => Results.Stream(
            stream => store.ExportAsync(stream, context.Request.QueryString.Value, context.RequestAborted),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "customers.xlsx"));
        endpoint.RequireAuthorization();
        return endpoint;
    }
}
