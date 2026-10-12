using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace IzzyBlazor;

/// <summary>
/// A row of overlapping avatars, such as the people on a project. With <see cref="Max"/>, the
/// rest collapse into a "+N" avatar.
/// </summary>
/// <example>
/// <code>
/// &lt;AvatarGroup Max="3" Label="Project members"&gt;
///     @foreach (var person in Members)
///     {
///         &lt;Avatar Name="@person.Name" Src="@person.Photo" /&gt;
///     }
/// &lt;/AvatarGroup&gt;
/// </code>
/// </example>
/// <remarks>
/// Put the avatars directly inside the group: <see cref="Max"/> hides the extra ones with CSS,
/// counting the group's children.
/// </remarks>
public class AvatarGroup : IzzyComponentBase
{
    private readonly List<Avatar> _avatars = [];

    /// <summary>How many avatars to show, from 1 to 10. The rest are counted in a "+N" avatar. Defaults to all of them.</summary>
    [Parameter] public int? Max { get; set; }

    /// <summary>The size of every avatar in the group. Defaults to <see cref="AvatarSize.Md"/>.</summary>
    [Parameter] public AvatarSize Size { get; set; } = AvatarSize.Md;

    /// <summary>The group's accessible name, such as "Project members".</summary>
    [Parameter] public string? Label { get; set; }

    /// <summary>
    /// The "+N" avatar's accessible name, with <c>{0}</c> for the number. Defaults to "{0} more".
    /// </summary>
    [Parameter] public string MoreLabel { get; set; } = "{0} more";

    /// <summary>The avatars.</summary>
    [Parameter] public RenderFragment? ChildContent { get; set; }

    internal int? ClampedMax => Max is { } max ? Math.Clamp(max, 1, 10) : null;

    internal int Count => _avatars.Count;

    internal void Register(Avatar avatar) => _avatars.Add(avatar);

    internal void Unregister(Avatar avatar) => _avatars.Remove(avatar);

    /// <inheritdoc />
    protected override void BuildAttributes(ClassBuilder classes, StyleBuilder styles)
    {
        classes.Add("ib-avatar-group");
        classes.Add($"ib-avatar-group-{Size.ToString().ToLowerInvariant()}", Size != AvatarSize.Md);
    }

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        var (cls, style) = BuildRootAttributes();
        builder.OpenElement(0, "div");
        builder.AddMultipleAttributes(1, AdditionalAttributes);
        builder.AddAttribute(2, "class", cls);
        builder.AddAttribute(3, "style", style);
        builder.AddAttribute(4, "role", "group");
        builder.AddAttribute(5, "aria-label", Label);
        builder.AddAttribute(6, "data-max", ClampedMax);

        builder.OpenComponent<CascadingValue<AvatarGroup>>(7);
        builder.AddComponentParameter(8, "Value", this);
        builder.AddComponentParameter(9, "IsFixed", true);
        builder.AddComponentParameter(10, "ChildContent", ChildContent);
        builder.CloseComponent();

        // Rendered after the avatars, so they have all registered by the time it counts them.
        if (ClampedMax is not null)
        {
            builder.OpenComponent<AvatarGroupMore>(11);
            builder.AddComponentParameter(12, nameof(AvatarGroupMore.Group), this);
            builder.CloseComponent();
        }

        builder.CloseElement();
    }
}

/// <summary>The "+N" avatar at the end of an <see cref="AvatarGroup"/>.</summary>
internal sealed class AvatarGroupMore : ComponentBase
{
    [Parameter] public AvatarGroup Group { get; set; } = default!;

    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        var hidden = Group.Count - (Group.ClampedMax ?? Group.Count);
        if (hidden <= 0)
        {
            return;
        }

        var size = Group.Size == AvatarSize.Md ? "" : $" ib-avatar-{Group.Size.ToString().ToLowerInvariant()}";
        builder.OpenElement(0, "span");
        builder.AddAttribute(1, "class", "ib-avatar ib-avatar-more" + size);
        builder.AddAttribute(2, "role", "img");
        builder.AddAttribute(3, "aria-label", string.Format(System.Globalization.CultureInfo.CurrentCulture, Group.MoreLabel, hidden));
        builder.OpenElement(4, "span");
        builder.AddAttribute(5, "class", "ib-avatar-fallback");
        builder.AddContent(6, $"+{hidden}");
        builder.CloseElement();
        builder.CloseElement();
    }
}
