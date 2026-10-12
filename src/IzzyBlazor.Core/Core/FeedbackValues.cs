namespace IzzyBlazor;

/// <summary>How an <see cref="Alert"/> or toast is drawn.</summary>
public enum AlertVariant
{
    /// <summary>A tinted background with a matching border (the default).</summary>
    Soft,

    /// <summary>A border in the status color on the page surface.</summary>
    Outline,

    /// <summary>A solid status-colored background.</summary>
    Solid,
}

/// <summary>Where a <see cref="ToastRegion"/> stacks its toasts. Start and End follow the reading direction.</summary>
public enum ToastPlacement
{
    /// <summary>Top corner on the start side.</summary>
    TopStart,

    /// <summary>Top center.</summary>
    TopCenter,

    /// <summary>Top corner on the end side.</summary>
    TopEnd,

    /// <summary>Bottom corner on the start side.</summary>
    BottomStart,

    /// <summary>Bottom center.</summary>
    BottomCenter,

    /// <summary>Bottom corner on the end side (the default).</summary>
    BottomEnd,
}

internal static class FeedbackNames
{
    public static string Name(this ToastPlacement placement) => placement switch
    {
        ToastPlacement.TopStart => "top-start",
        ToastPlacement.TopCenter => "top-center",
        ToastPlacement.TopEnd => "top-end",
        ToastPlacement.BottomStart => "bottom-start",
        ToastPlacement.BottomCenter => "bottom-center",
        ToastPlacement.BottomEnd => "bottom-end",
        _ => throw new ArgumentOutOfRangeException(nameof(placement)),
    };
}
