using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace IzzyBlazor;

/// <summary>
/// Shared plumbing for IzzyBlazor form fields, built on Blazor's <see cref="InputBase{TValue}"/>
/// so the same field binds on static form posts (through its <c>name</c>) and on interactive pages.
/// </summary>
/// <remarks>
/// The label, required state and native validation attributes come from the bound model's
/// DataAnnotations when not set explicitly: <c>[Display(Name)]</c> becomes the label,
/// <c>[Required]</c> becomes <c>required</c>, <c>[StringLength]</c> becomes <c>maxlength</c>.
/// The browser checks those before posting; the server still validates.
/// </remarks>
public abstract class FieldBase<TValue> : InputBase<TValue>
{
    [Inject] private IServiceProvider IzzyServices { get; set; } = default!;

    /// <summary>
    /// The default text for labels and messages: the app's registered <see cref="IzzyText"/>, or
    /// English. A component's own text parameters win over it.
    /// </summary>
    protected IzzyText TextDefaults => _textDefaults ??= IzzyText.From(IzzyServices);

    private IzzyText? _textDefaults;

    private string? _generatedId;
    private MemberInfo? _member;
    private Expression? _memberSource;

    /// <summary>The visible label. Defaults to the model property's <c>[Display(Name)]</c>, or its name.</summary>
    [Parameter] public string? Label { get; set; }

    /// <summary>Help text shown under the field and linked to it for screen readers.</summary>
    [Parameter] public string? Hint { get; set; }

    /// <summary>Whether a value is required. Defaults to whether the property has <c>[Required]</c>.</summary>
    [Parameter] public bool? Required { get; set; }

    /// <summary>Disables the field. Disabled fields are not posted with the form.</summary>
    [Parameter] public bool Disabled { get; set; }

    /// <summary>Makes the field read-only. Read-only fields are still posted.</summary>
    [Parameter] public bool ReadOnly { get; set; }

    /// <summary>Height, shared with buttons. Defaults to <see cref="ControlSize.Md"/>.</summary>
    [Parameter] public ControlSize Size { get; set; } = ControlSize.Md;

    /// <summary>Classes for the field's outer wrapper (label, control, hint and error).</summary>
    [Parameter] public string? Class { get; set; }

    /// <summary>The control's element id. Generated when not set; also accepted as an <c>id</c> attribute.</summary>
    protected string FieldId =>
        AdditionalAttributes?.TryGetValue("id", out var id) == true && id is string s && s.Length > 0
            ? s
            : _generatedId ??= IdGenerator.Next("ib-field");

    /// <summary>The hint element's id.</summary>
    protected string HintId => FieldId + "-hint";

    /// <summary>The error element's id.</summary>
    protected string ErrorId => FieldId + "-error";

    /// <summary>The current validation messages for this field.</summary>
    protected IReadOnlyList<string> Errors =>
        EditContext is null ? [] : EditContext.GetValidationMessages(FieldIdentifier).ToList();

    /// <summary>Whether the field has validation errors.</summary>
    protected bool Invalid => Errors.Count > 0;

    /// <summary>The ids that describe the control: the hint and, when invalid, the errors.</summary>
    protected string? DescribedBy
    {
        get
        {
            var ids = new List<string>(2);
            if (!string.IsNullOrWhiteSpace(Hint)) ids.Add(HintId);
            if (Invalid) ids.Add(ErrorId);
            return ids.Count == 0 ? null : string.Join(' ', ids);
        }
    }

    /// <summary>The label to show.</summary>
    protected string? ResolvedLabel =>
        Label
        ?? DisplayName
        ?? Attribute<DisplayAttribute>()?.GetName()
        ?? Attribute<DisplayNameAttribute>()?.DisplayName
        ?? Humanize(FieldIdentifier.FieldName);

    /// <summary>Whether the field is required.</summary>
    protected bool IsRequired => Required ?? Attribute<RequiredAttribute>() is not null;

    /// <summary>The maximum length from <c>[StringLength]</c> or <c>[MaxLength]</c>, if any.</summary>
    protected int? AnnotatedMaxLength
    {
        get
        {
            if (Attribute<StringLengthAttribute>() is { MaximumLength: > 0 } stringLength) return stringLength.MaximumLength;
            if (Attribute<MaxLengthAttribute>() is { Length: > 0 } maxLength) return maxLength.Length;
            return null;
        }
    }

    /// <summary>The minimum length from <c>[StringLength]</c> or <c>[MinLength]</c>, if any.</summary>
    protected int? AnnotatedMinLength
    {
        get
        {
            if (Attribute<StringLengthAttribute>() is { MinimumLength: > 0 } stringLength) return stringLength.MinimumLength;
            if (Attribute<MinLengthAttribute>() is { Length: > 0 } minLength) return minLength.Length;
            return null;
        }
    }

    /// <summary>Gets a DataAnnotations attribute from the bound property.</summary>
    protected TAttribute? Attribute<TAttribute>() where TAttribute : Attribute
    {
        if (!ReferenceEquals(_memberSource, ValueExpression))
        {
            _memberSource = ValueExpression;
            _member = ValueExpression?.Body switch
            {
                MemberExpression m => m.Member,
                UnaryExpression { Operand: MemberExpression m } => m.Member,
                _ => null,
            };
        }

        return _member?.GetCustomAttribute<TAttribute>(inherit: true);
    }

    /// <summary>Attributes other than <c>id</c>, for the native control.</summary>
    protected IEnumerable<KeyValuePair<string, object>>? ControlAttributes =>
        AdditionalAttributes?.Where(a => !string.Equals(a.Key, "id", StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// A name for a group of radios: the field's form name, or its id where Blazor gives none
    /// (WebAssembly pages don't generate field names). Radios need one to be a single group for
    /// arrow keys and <c>required</c>.
    /// </summary>
    protected string GroupName => NameAttributeValue is { Length: > 0 } name ? name : FieldId;

    /// <summary>The wrapper's classes.</summary>
    protected string WrapperClass(string kind) => new ClassBuilder()
        .Add("ib-field")
        .Add($"ib-field-{kind}")
        .Add("ib-field-sm", Size == ControlSize.Sm)
        .Add("ib-field-lg", Size == ControlSize.Lg)
        .Add("ib-field-invalid", Invalid)
        .Add("ib-field-disabled", Disabled)
        .Add(Class)
        .Build()!;

    /// <inheritdoc />
    protected override string? FormatValueAsString(TValue? value) =>
        BindConverter.FormatValue(value, CultureInfo.CurrentCulture)?.ToString();

    /// <inheritdoc />
    protected override bool TryParseValueFromString(string? value, [MaybeNullWhen(false)] out TValue result, [NotNullWhen(false)] out string? validationErrorMessage)
    {
        if (BindConverter.TryConvertTo<TValue>(value, CultureInfo.CurrentCulture, out result))
        {
            validationErrorMessage = null;
            return true;
        }

        validationErrorMessage = $"The {ResolvedLabel ?? FieldIdentifier.FieldName} field is not valid.";
        return false;
    }

    /// <summary><c>FirstName</c> becomes <c>First name</c>.</summary>
    private static string? Humanize(string? name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return null;
        }

        var sb = new System.Text.StringBuilder(name.Length + 4);
        for (var i = 0; i < name.Length; i++)
        {
            var c = name[i];
            if (i > 0 && char.IsUpper(c) && !char.IsUpper(name[i - 1]))
            {
                sb.Append(' ').Append(char.ToLowerInvariant(c));
            }
            else
            {
                sb.Append(c);
            }
        }

        return sb.ToString();
    }
}

/// <summary>How a <see cref="Form"/> lays out labels and fields.</summary>
public enum FormLayout
{
    /// <summary>Labels above fields (the default).</summary>
    Vertical,

    /// <summary>Labels in a column to the left of fields, from 640px up.</summary>
    Horizontal,
}

/// <summary>The kind of text a <see cref="TextField"/> takes. Sets the input type and the mobile keyboard.</summary>
public enum TextFieldType
{
    /// <summary>Plain text.</summary>
    Text,

    /// <summary>An email address.</summary>
    Email,

    /// <summary>A password, masked, with a show/hide toggle.</summary>
    Password,

    /// <summary>A search query.</summary>
    Search,

    /// <summary>A phone number.</summary>
    Tel,

    /// <summary>A URL.</summary>
    Url,
}
