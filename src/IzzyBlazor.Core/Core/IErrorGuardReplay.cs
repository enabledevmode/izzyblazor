namespace IzzyBlazor;

/// <summary>
/// Lets a static page that failed after an <see cref="ErrorGuard"/> caught an exception render again
/// with that guard's message in place. ASP.NET Core fails the request when the content a boundary
/// removes held a named form (dotnet/aspnetcore#70640); the server package renders the page a second
/// time, and each guard asks here whether it caught something the first time. Guards are told apart
/// by the order they join in, which is the same on both renders of one request.
/// </summary>
internal interface IErrorGuardReplay
{
    /// <summary>A guard's place among the guards of this render.</summary>
    int Join();

    /// <summary>Records what the guard at <paramref name="index"/> caught.</summary>
    void Caught(int index, Exception exception);

    /// <summary>On the second render, what the guard at <paramref name="index"/> caught on the first; else null.</summary>
    Exception? CaughtBefore(int index);
}
