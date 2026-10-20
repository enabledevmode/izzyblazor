using Microsoft.AspNetCore.Components;

namespace IzzyBlazor;

// Names removed in 0.1.0-alpha.11, kept for one release so old markup fails to build instead of
// rendering as unknown HTML elements or attributes. They go in a later release.

/// <summary>Replaced by <see cref="Button"/> with <c>Href</c>.</summary>
[Obsolete("LinkButton is now <Button Href=\"...\">. LinkButton goes in a later release.", error: true)]
public sealed class LinkButton : Button
{
}

/// <summary>Replaced by <see cref="SiteNav"/>.</summary>
[Obsolete("HeaderNav is now <SiteNav>, its PanelFooter <SiteNavFooter>; the panel opens under the header, so PanelHeader is gone. HeaderNav goes in a later release.", error: true)]
public sealed class HeaderNav : SiteNav
{
}

/// <summary>Replaced by <see cref="DialogFooter"/>.</summary>
[Obsolete("DialogActions is now <DialogFooter>. DialogActions goes in a later release.", error: true)]
public sealed class DialogActions : DialogFooter
{
}

/// <summary>
/// Throws for a removed slot written as a child element (<c>&lt;Footer&gt;</c>). Razor passes those
/// by name, so the <see cref="ObsoleteAttribute"/> on the parameter only catches the attribute form.
/// </summary>
internal static class RemovedSlots
{
    public static void Check(ParameterView parameters, string component, params (string Name, string Replacement)[] slots)
    {
        foreach (var (name, replacement) in slots)
        {
            if (parameters.TryGetValue<object>(name, out var value) && value is not null)
            {
                throw new InvalidOperationException(
                    $"{component}'s {name} was replaced by <{replacement}> in 0.1.0-alpha.11: write <{replacement}> among the {component}'s content instead of <{name}>.");
            }
        }
    }
}
