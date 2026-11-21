using System.ComponentModel.DataAnnotations;
using {{RootNamespace}}.Auditing;

namespace {{RootNamespace}}.Data;

/// <summary>An opportunity with a company: what it's worth, where it is in the pipeline and when it should close.</summary>
public class Deal : IAudited
{
    public int Id { get; set; }

    [Required, StringLength(120)] public string Name { get; set; } = "";

    public int CompanyId { get; set; }
    public Company? Company { get; set; }

    /// <summary>The person at the company the deal is with.</summary>
    public int? ContactId { get; set; }
    public Contact? Contact { get; set; }

    public string? OwnerId { get; set; }
    public ApplicationUser? Owner { get; set; }

    /// <summary>Its value, in the workspace's currency.</summary>
    [Range(0, 1_000_000_000)] public decimal Value { get; set; }

    public DealStage Stage { get; set; }

    public DateOnly ExpectedClose { get; set; }

    public DealSource? Source { get; set; }

    /// <summary>What's included, and what needs to happen next.</summary>
    [StringLength(4000)] public string? Scope { get; set; }

    public DateOnly Created { get; set; }

    /// <summary>The day it was won or lost; null while it's open.</summary>
    public DateOnly? Closed { get; set; }

    /// <summary>Why it was lost, asked as it's marked lost; null for any other deal.</summary>
    public DealLostReason? LostReason { get; set; }

    /// <summary>Still in the pipeline: not won, not lost.</summary>
    public bool IsOpen => Stage < DealStage.Won;

    string IAudited.AuditName => Name;
}

/// <summary>The pipeline's stages in order, then how a deal ends.</summary>
public enum DealStage { Discovery, Qualified, Proposal, Negotiation, Won, Lost }

/// <summary>Why a deal was lost, in the order the form offers them.</summary>
public enum DealLostReason
{
    Price,
    Timing,
    [Display(Name = "Chose a competitor")] Competitor,
    [Display(Name = "No decision")] NoDecision,
    [Display(Name = "Not a fit")] NotAFit,
    Other,
}

public enum DealSource
{
    Referral,
    Website,
    [Display(Name = "Existing customer")] ExistingCustomer,
    Outbound,
}
