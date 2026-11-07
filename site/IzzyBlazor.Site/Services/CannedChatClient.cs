using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;

namespace IzzyBlazor.Site.Services;

/// <summary>
/// The docs' stand-in for a model: canned cooking answers, streamed a few characters at a time,
/// so the Chat examples show the real <see cref="IChatClient"/> recipe without an API key.
/// </summary>
public sealed class CannedChatClient : IChatClient
{
    private static readonly (string[] Words, string Answer)[] Answers =
    [
        (["rice"], """
            For fluffy long-grain rice:

            1. Rinse **1 cup** of rice until the water runs clear.
            2. Simmer it with **1½ cups** of water, covered, for 15 minutes.
            3. Rest it off the heat for 10 minutes, then fluff with a fork.
            """),
        (["egg", "boil"], """
            | Yolk | Minutes in boiling water |
            | --- | ---: |
            | Runny | 6 |
            | Jammy | 7 |
            | Firm | 10 |

            Cool them in ice water so they peel easily.
            """),
        (["substitute", "instead", "buttermilk"], """
            No buttermilk? Stir **1 tablespoon** of lemon juice or white vinegar into **1 cup** of milk and wait 5 minutes.
            """),
    ];

    /// <inheritdoc />
    public async Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        var text = "";
        await foreach (var update in GetStreamingResponseAsync(messages, options, cancellationToken)) text += update.Text;
        return new ChatResponse(new ChatMessage(ChatRole.Assistant, text));
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var question = messages.LastOrDefault(m => m.Role == ChatRole.User)?.Text.ToLowerInvariant() ?? "";
        var answer = Answers.FirstOrDefault(a => a.Words.Any(question.Contains)).Answer
            ?? "I can help with rice, boiled eggs and substitutions. What are you cooking?";
        await Task.Delay(600, cancellationToken);
        for (var i = 0; i < answer.Length; i += 6)
        {
            yield return new ChatResponseUpdate(ChatRole.Assistant, answer.Substring(i, Math.Min(6, answer.Length - i)));
            await Task.Delay(25, cancellationToken);
        }
    }

    /// <inheritdoc />
    public object? GetService(Type serviceType, object? serviceKey = null) => serviceType.IsInstanceOfType(this) ? this : null;

    /// <inheritdoc />
    public void Dispose()
    {
    }
}
