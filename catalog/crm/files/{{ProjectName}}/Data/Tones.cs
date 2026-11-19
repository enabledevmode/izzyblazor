using IzzyBlazor;

namespace {{RootNamespace}}.Data;

/// <summary>The badge color for a record's state, the same on every page that shows it.</summary>
public static class Tones
{
    /// <summary>A contact's status: a customer in green, a prospect in blue, a lead plain.</summary>
    public static Status Of(ContactStatus status) => status switch
    {
        ContactStatus.Customer => Status.Success,
        ContactStatus.Prospect => Status.Info,
        _ => Status.Neutral,
    };

    /// <summary>A company's status: a customer in green, a prospect in blue, an inactive one plain.</summary>
    public static Status Of(CompanyStatus status) => status switch
    {
        CompanyStatus.Customer => Status.Success,
        CompanyStatus.Prospect => Status.Info,
        _ => Status.Neutral,
    };

    /// <summary>A deal's stage: a proposal out in blue, won in green, lost in red, the rest plain.</summary>
    public static Status Of(DealStage stage) => stage switch
    {
        DealStage.Proposal => Status.Info,
        DealStage.Won => Status.Success,
        DealStage.Lost => Status.Danger,
        _ => Status.Neutral,
    };
}
