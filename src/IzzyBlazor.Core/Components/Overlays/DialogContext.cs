namespace IzzyBlazor;

/// <summary>What a <see cref="Dialog"/> shares with the components inside it.</summary>
/// <param name="Id">The dialog element's id.</param>
/// <param name="HasCloseForm">
/// Whether the dialog has its own <c>method="dialog"</c> form to close with. False when it's
/// inside another form (<see cref="Dialog.InsideForm"/>), where close buttons use the native
/// <c>close</c> command and must not submit.
/// </param>
public sealed record DialogContext(string Id, bool HasCloseForm = true);
