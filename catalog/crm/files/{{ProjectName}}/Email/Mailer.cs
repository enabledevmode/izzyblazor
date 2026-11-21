using System.Net;
using System.Net.Mail;
using System.Text;
using Microsoft.Extensions.Options;

namespace {{RootNamespace}}.Email;

/// <summary>
/// Where mail goes: an SMTP server, which every mail service has (Postmark, SendGrid, Amazon SES, Mailgun, Resend,
/// or your own). Set it in configuration; until Host and From are set, nothing is sent.
/// <code>
/// Email__Host=smtp.postmarkapp.com
/// Email__Username=…
/// Email__Password=…
/// Email__From=crm@example.com        an address the service lets you send from
/// Email__FromName="Acme Sales"       the name beside it; the workspace's name without it
/// Email__Port=587                    587 without it
/// Email__AppUrl=https://crm.example.com   where the app is, for the links in its mail. Set it: without it an
///                                         invitation or reset link uses the address the request came to, which
///                                         a proxy that passes any Host header lets a stranger choose.
/// </code>
/// </summary>
public sealed class EmailOptions
{
    public string? Host { get; set; }

    /// <summary>587, where a server takes mail from apps and starts TLS.</summary>
    public int Port { get; set; } = 587;

    public string? Username { get; set; }

    public string? Password { get; set; }

    public string? From { get; set; }

    public string? FromName { get; set; }

    /// <summary>The app's own address, such as https://crm.example.com: the links in its mail start with it.</summary>
    public string? AppUrl { get; set; }

    /// <summary>Whether the connection is encrypted (STARTTLS). Off only for a test server on your own machine.</summary>
    public bool Tls { get; set; } = true;
}

/// <summary>
/// Sends the app's mail: invitations and password resets (IdentityEmails.cs) and activity reminders
/// (Reminders.cs). To send another way (a service's own API), change <see cref="SendAsync(string, string, string, string)"/>:
/// everything goes through it.
/// </summary>
public sealed class Mailer(IOptions<EmailOptions> options, ILogger<Mailer> log)
{
    private readonly EmailOptions _options = options.Value;

    /// <summary>Whether mail is set up. Without it the team page hands the admin each link to pass on.</summary>
    public bool Sends => !string.IsNullOrWhiteSpace(_options.Host) && !string.IsNullOrWhiteSpace(_options.From);

    /// <summary>
    /// The address of one of the app's pages, such as "Account/ResetPassword", for a link in an email: under
    /// Email:AppUrl when it's set, else under the address this request came to (<paramref name="request"/>).
    /// </summary>
    public string PageUrl(string page, Uri request) =>
        _options.AppUrl is { Length: > 0 } app ? $"{app.TrimEnd('/')}/{page}" : new Uri(request, page).AbsoluteUri;

    /// <summary>
    /// Sends a message of a few paragraphs with one link under them (when there is one), as text and as HTML. False when it
    /// couldn't be sent: the log says why.
    /// </summary>
    /// <param name="from">The name it's from, such as the workspace's, when Email:FromName isn't set.</param>
    public Task<bool> SendAsync(string to, string subject, string from, IReadOnlyList<string> paragraphs, string linkText, string? link, string? after = null)
    {
        var text = new StringBuilder();
        var html = new StringBuilder("<div style=\"font-family:system-ui,-apple-system,'Segoe UI',sans-serif;font-size:16px;line-height:1.5;color:#1f2933;max-width:32rem\">");
        foreach (var paragraph in paragraphs)
        {
            text.AppendLine(paragraph).AppendLine();
            html.Append("<p>").Append(WebUtility.HtmlEncode(paragraph)).Append("</p>");
        }

        if (link is not null)
        {
            text.AppendLine($"{linkText}: {link}");
            html.Append("<p><a href=\"").Append(WebUtility.HtmlEncode(link)).Append("\">").Append(WebUtility.HtmlEncode(linkText)).Append("</a></p>");
        }

        if (after is not null)
        {
            text.AppendLine().AppendLine(after);
            html.Append("<p style=\"color:#52606d;font-size:14px\">").Append(WebUtility.HtmlEncode(after)).Append("</p>");
        }

        html.Append("</div>");
        return SendAsync(to, subject, from, text.ToString(), html.ToString());
    }

    /// <summary>Sends one message. False when it couldn't be sent: the log says why.</summary>
    public async Task<bool> SendAsync(string to, string subject, string from, string text, string? html = null)
    {
        if (!Sends)
        {
            log.LogWarning("Not sent to {To}: “{Subject}”. No mail is sent until Email:Host and Email:From are set (Email/Mailer.cs).", to, subject);
            return false;
        }

        try
        {
            using var message = new MailMessage
            {
                // One line each: a header can't hold a line break, wherever the words came from.
                From = new MailAddress(_options.From!, OneLine(string.IsNullOrWhiteSpace(_options.FromName) ? from : _options.FromName)),
                Subject = OneLine(subject), Body = text, BodyEncoding = Encoding.UTF8, SubjectEncoding = Encoding.UTF8,
            };
            message.To.Add(to);
            if (html is not null) message.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(html, Encoding.UTF8, "text/html"));

            using var client = new SmtpClient(_options.Host, _options.Port) { EnableSsl = _options.Tls };
            if (!string.IsNullOrEmpty(_options.Username)) client.Credentials = new NetworkCredential(_options.Username, _options.Password);
            await client.SendMailAsync(message);
            return true;
        }
        // Whatever went wrong (the server, its certificate, an address): the caller is told it wasn't sent.
        catch (Exception problem) when (problem is not OperationCanceledException)
        {
            log.LogError(problem, "Couldn't send “{Subject}” to {To} through {Host}:{Port}.", subject, to, _options.Host, _options.Port);
            return false;
        }
    }

    private static string OneLine(string text) => text.ReplaceLineEndings(" ").Trim();
}
