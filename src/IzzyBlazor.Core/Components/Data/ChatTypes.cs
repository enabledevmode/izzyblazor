using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace IzzyBlazor;

/// <summary>Where a <see cref="Chat"/>'s reply is.</summary>
public enum ChatStatus
{
    /// <summary>Nothing is on its way: Send sends.</summary>
    Ready,

    /// <summary>A message was sent and no reply has started: Stop shows in Send's place.</summary>
    Waiting,

    /// <summary>A reply is arriving: Stop shows in Send's place.</summary>
    Streaming,

    /// <summary>The last reply failed. Send sends again.</summary>
    Failed,
}

/// <summary>What a person sent in a <see cref="Chat"/>.</summary>
/// <param name="Text">The message, as typed (or the suggestion chosen), trimmed.</param>
/// <param name="CanStream">
/// Whether the reply can arrive in pieces: on an interactive page, and on a static page whose
/// post came with script (with <c>[StreamRendering]</c> on the page). With JavaScript off it's
/// false: send the reply whole, since a streamed one would stop at its first piece there.
/// </param>
public sealed record ChatSendArgs(string Text, bool CanStream);

/// <summary>
/// A Chat's entries in the order they arrived, which is page order as a conversation only grows:
/// each knows the one before it (for day separators), and a static Retry names its entry by place.
/// </summary>
internal sealed class ChatContext(Chat owner)
{
    private readonly List<ChatEntry> _entries = [];

    public Chat Owner { get; } = owner;

    public void Register(ChatEntry entry) => _entries.Add(entry);

    public void Unregister(ChatEntry entry) => _entries.Remove(entry);

    public int IndexOf(ChatEntry entry) => _entries.IndexOf(entry);

    public ChatEntry? At(int index) => index >= 0 && index < _entries.Count ? _entries[index] : null;

    public ChatEntry? Before(ChatEntry entry)
    {
        var index = _entries.IndexOf(entry);
        return index > 0 ? _entries[index - 1] : null;
    }
}

/// <summary>A Chat's static post: the text typed, a suggestion chosen, Stop, or an entry's Retry. Its own part, in the Chat's form scope.</summary>
internal sealed class ChatPost : ComponentBase
{
    internal const string TextName = "ib-chat-text";
    internal const string SuggestionName = "ib-chat-suggestion";
    internal const string StopName = "ib-chat-stop";
    internal const string RetryName = "ib-chat-retry";
    internal const string ScriptName = "ib-chat-script";

    [SupplyParameterFromForm(Name = TextName)]
    public string? Text { get; set; }

    [SupplyParameterFromForm(Name = SuggestionName)]
    public string? Suggestion { get; set; }

    [SupplyParameterFromForm(Name = StopName)]
    public string? Stop { get; set; }

    [SupplyParameterFromForm(Name = RetryName)]
    public int? Retry { get; set; }

    // Added by the chat's script to what the form posts: the page has script, so it can stream.
    [SupplyParameterFromForm(Name = ScriptName)]
    public string? Script { get; set; }

    [Parameter] public RenderFragment<ChatPost>? ChildContent { get; set; }

    protected override void BuildRenderTree(RenderTreeBuilder builder) => builder.AddContent(0, ChildContent, this);
}
