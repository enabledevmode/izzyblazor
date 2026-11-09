namespace IzzyBlazor;

/// <summary>What a Scheduler's <c>AppointmentTemplate</c> draws for: an appointment, where.</summary>
/// <typeparam name="TItem">The type of the appointments.</typeparam>
/// <param name="Item">The appointment.</param>
/// <param name="View">The view it's drawn in.</param>
/// <param name="OneLine">Whether it has one line: a box under 45 minutes, an all-day bar or a month's row. Otherwise it's a taller box or an agenda row, with room to wrap.</param>
public sealed record SchedulerAppointmentContext<TItem>(TItem Item, SchedulerView View, bool OneLine);
