#if (OverHttp)
using {{StoreNamespace}};

#endif
namespace {{RootNamespace}}.Data;

/// <summary>
/// The {{ModelPluralLower}} as an Excel file, for the grid's Export, from izzy add crud: the rows the grid shows,
/// with its search, sort and columns, which it adds to the address.
/// </summary>
public static class {{Model}}Export
{
    public static IEndpointConventionBuilder Map(IEndpointRouteBuilder app)
    {
        var endpoint = app.MapGet("/{{Route}}/export.xlsx", (HttpContext context, I{{Model}}Store store) => Results.Stream(
            stream => store.ExportAsync(stream, context.Request.QueryString.Value, context.RequestAborted),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "{{FileName}}.xlsx"));
#if (HasViewPolicy)
        endpoint.RequireAuthorization("{{ViewPolicy}}");
#elif (Authorize)
        endpoint.RequireAuthorization();
#endif
        return endpoint;
    }
}
