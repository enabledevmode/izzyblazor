using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace IzzyBlazor;

/// <summary>What the guards of one request caught, kept on the HttpContext so it outlives the first render's services.</summary>
internal sealed class ErrorGuardReplayState
{
    private static readonly object Key = new();

    public Dictionary<int, Exception> Caught { get; } = [];
    public Exception? First { get; set; }
    public int Joined { get; set; }
    public bool Replaying { get; set; }

    public static ErrorGuardReplayState? Find(HttpContext context) =>
        context.Items.TryGetValue(Key, out var state) ? state as ErrorGuardReplayState : null;

    public static ErrorGuardReplayState For(HttpContext context)
    {
        if (Find(context) is not { } state)
        {
            context.Items[Key] = state = new ErrorGuardReplayState();
        }

        return state;
    }
}

internal sealed class HttpErrorGuardReplay(IHttpContextAccessor http) : IErrorGuardReplay
{
    private ErrorGuardReplayState? State => http.HttpContext is { } context ? ErrorGuardReplayState.For(context) : null;

    public int Join() => State is { } state ? state.Joined++ : -1;

    public void Caught(int index, Exception exception)
    {
        if (index >= 0 && State is { Replaying: false } state)
        {
            state.Caught[index] = exception;
            state.First ??= exception;
        }
    }

    public Exception? CaughtBefore(int index) =>
        State is { Replaying: true } state && state.Caught.TryGetValue(index, out var exception) ? exception : null;
}

/// <summary>
/// Renders a static page again when ASP.NET Core failed it after an ErrorGuard caught an exception
/// (dotnet/aspnetcore#70640: the content the guard removed held a named form). The second render shows
/// the guard's message where the content was. Only a GET is rendered again: a post could run its
/// handler twice, so it fails with the exception the guard caught, not the framework's. Runs from
/// UseExceptionHandler and from the developer exception page.
/// </summary>
internal sealed class ErrorGuardRerender : IExceptionHandler, IDeveloperPageExceptionFilter
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        // UseExceptionHandler has cleared the endpoint and route values by now; its feature kept them.
        var feature = httpContext.Features.Get<IExceptionHandlerFeature>();
        return await TryRerender(httpContext, exception, feature?.Endpoint, feature?.RouteValues, feature?.Path);
    }

    public async Task HandleExceptionAsync(ErrorContext errorContext, Func<ErrorContext, Task> next)
    {
        var context = errorContext.HttpContext;
        if (await TryRerender(context, errorContext.Exception, context.GetEndpoint(), context.Features.Get<IRouteValuesFeature>()?.RouteValues, null))
        {
            return;
        }

        // Not rendered again: show the exception the guard caught, which is the one to fix.
        var caught = IsNamedFormFailure(errorContext.Exception) ? ErrorGuardReplayState.Find(context)?.First : null;
        await next(caught is null ? errorContext : new ErrorContext(context, caught));
    }

    private static async Task<bool> TryRerender(HttpContext context, Exception exception, Endpoint? endpoint, RouteValueDictionary? routeValues, string? path)
    {
        if (ErrorGuardReplayState.Find(context) is not { Replaying: false, Caught.Count: > 0 } state
            || endpoint?.RequestDelegate is not { } render
            || context.Response.HasStarted
            || !(HttpMethods.IsGet(context.Request.Method) || HttpMethods.IsHead(context.Request.Method))
            || !IsNamedFormFailure(exception))
        {
            return false;
        }

        state.Replaying = true;
        state.Joined = 0;

        var handlerFeature = context.Features.Get<IExceptionHandlerFeature>();
        var pathFeature = context.Features.Get<IExceptionHandlerPathFeature>();
        var routeFeature = context.Features.Get<IRouteValuesFeature>();
        var (oldEndpoint, oldRouteValues, oldPath, oldStatus, oldServices) =
            (context.GetEndpoint(), routeFeature?.RouteValues, context.Request.Path, context.Response.StatusCode, context.RequestServices);

        // A fresh set of scoped services (the first render's renderer is spent), and no sign of error
        // handling, so the page renders as it would on any request.
        await using var scope = context.RequestServices.GetRequiredService<IServiceScopeFactory>().CreateAsyncScope();
        try
        {
            context.RequestServices = scope.ServiceProvider;
            context.Features.Set<IExceptionHandlerFeature>(null);
            context.Features.Set<IExceptionHandlerPathFeature>(null);
            context.Response.Clear();
            context.Response.StatusCode = StatusCodes.Status200OK;
            context.SetEndpoint(endpoint);
            if (routeFeature is not null && routeValues is not null)
            {
                routeFeature.RouteValues = routeValues;
            }

            if (path is not null)
            {
                context.Request.Path = path;
            }

            await render(context);
            return true;
        }
        catch (Exception) when (!context.Response.HasStarted)
        {
            context.Response.Clear();
            context.Response.StatusCode = oldStatus;
            context.SetEndpoint(oldEndpoint);
            if (routeFeature is not null)
            {
                routeFeature.RouteValues = oldRouteValues!;
            }

            context.Request.Path = oldPath;
            context.Features.Set(handlerFeature);
            context.Features.Set(pathFeature);
            return false;
        }
        finally
        {
            context.RequestServices = oldServices;
        }
    }

    // The framework's failure, whichever message it has: thrown while it updates named submit events.
    private static bool IsNamedFormFailure(Exception exception)
    {
        for (var e = exception; e is not null; e = e.InnerException)
        {
            if (e.StackTrace?.Contains("NamedSubmitEvent", StringComparison.Ordinal) == true)
            {
                return true;
            }
        }

        return false;
    }
}
