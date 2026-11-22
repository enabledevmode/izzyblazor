namespace IzzyBlazor.Generation.Tests;

/// <summary>A test that generates an account item, so it needs the private catalog; skipped where there is none.</summary>
public sealed class AccountFactAttribute : FactAttribute
{
    public const string Reason = "Needs the private catalog (IZZY_PRO_CATALOG, or izzyblazor-pro-catalog beside the repo).";

    public AccountFactAttribute()
    {
        if (ProjectBuild.PrivateCatalog is null) Skip = Reason;
    }
}

/// <inheritdoc cref="AccountFactAttribute"/>
public sealed class AccountTheoryAttribute : TheoryAttribute
{
    public AccountTheoryAttribute()
    {
        if (ProjectBuild.PrivateCatalog is null) Skip = AccountFactAttribute.Reason;
    }
}
