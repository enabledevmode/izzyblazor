namespace IzzyBlazor;

/// <summary>
/// A kind of appointment in a <see cref="Scheduler{TItem}"/>, such as Work or Family: its color,
/// and an item in the toolbar's Categories menu that shows or hides its appointments. An
/// appointment's category is the Scheduler's <c>Category</c> value that matches
/// <see cref="Value"/>. The Scheduler takes a list of them as <c>Categories</c>, written out or
/// made from data: <c>kinds.Select(k =&gt; new SchedulerCategory(k.Id, k.Name, k.Color))</c>.
/// <para><c>new SchedulerCategory("work", "Work", "var(--ib-chart-7)")</c></para>
/// </summary>
/// <param name="Value">What an appointment's <c>Category</c> gives for it, such as <c>"work"</c>: also its key in the page's address when hidden.</param>
/// <param name="Label">Its name in the Categories menu and the form, and what screen readers hear with its appointments. Defaults to <see cref="Value"/>.</param>
/// <param name="Color">Its appointments' color, a CSS color such as <c>var(--ib-chart-3)</c>, unless an appointment has its own. Defaults to the palette's next.</param>
public sealed record SchedulerCategory(string Value, string? Label = null, string? Color = null);
