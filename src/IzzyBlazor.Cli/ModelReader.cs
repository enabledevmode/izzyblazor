using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace IzzyBlazor.Cli;

/// <summary>What a field is shown and edited as, from its type, its attributes and its name.</summary>
enum FieldKind { Text, LongText, Email, Phone, Url, Integer, Decimal, Money, Bool, Date, DateTime, Time, Enum, Guid }

/// <summary>An enum's member: its name in code and the label people see.</summary>
sealed record EnumValue(string Name, string Label);

/// <summary>
/// A property of the model, as crud shows and edits it.
/// </summary>
/// <param name="Name">The property's name.</param>
/// <param name="Label">From <c>[Display(Name)]</c>, else the name in words: LifetimeValue is "Lifetime value".</param>
/// <param name="ShortLabel">A column's title: <c>[Display(ShortName)]</c>, else the label.</param>
/// <param name="Type">The property's type as written, such as <c>decimal?</c> or <c>CustomerStatus</c>.</param>
/// <param name="Required">Has to have a value: <c>[Required]</c>, <c>required</c>, a value type that isn't nullable, or a reference type that isn't nullable where nullable is on.</param>
/// <param name="ReadOnly">Shown but not edited: no public setter, <c>[Editable(false)]</c> or a value the database makes.</param>
/// <param name="MaxLength">From <c>[StringLength]</c> or <c>[MaxLength]</c>; 0 when there's none.</param>
/// <param name="Values">An enum's members, for its Select; empty otherwise.</param>
sealed record ModelField(string Name, string Label, string ShortLabel, FieldKind Kind, string Type, bool Required, bool ReadOnly, int MaxLength, IReadOnlyList<EnumValue> Values)
{
    /// <summary>An enum's namespace, where it's declared; empty otherwise or in the global namespace.</summary>
    public string EnumNamespace { get; init; } = "";

    public bool Nullable => Type.EndsWith('?');
    public bool Numeric => Kind is FieldKind.Integer or FieldKind.Decimal or FieldKind.Money;
}

/// <summary>A DbContext in the project that has a <c>DbSet</c> of the model.</summary>
sealed record ContextInfo(string Name, string Namespace, string SetName);

/// <summary>The model crud is generated for.</summary>
/// <param name="Name">The class or record's name.</param>
/// <param name="Namespace">Its namespace; empty for the global one.</param>
/// <param name="File">The file it's in, from the app's folder.</param>
/// <param name="Key">The key: <c>[Key]</c>, else Id, else {Model}Id.</param>
/// <param name="KeyGenerated">Whether the database makes the key (a whole number with no <c>[DatabaseGenerated(None)]</c>).</param>
/// <param name="Fields">The other properties crud shows, in the order written.</param>
/// <param name="Skipped">Properties left out, each with why.</param>
/// <param name="Contexts">The DbContexts with a DbSet of it.</param>
sealed record ModelInfo(string Name, string Namespace, string File, ModelField Key, bool KeyGenerated,
    IReadOnlyList<ModelField> Fields, IReadOnlyList<string> Skipped, IReadOnlyList<ContextInfo> Contexts);

/// <summary>
/// Reads a model class from the app's source, with no build: Roslyn's syntax trees, not a compilation, so
/// a project that doesn't build yet can still be read, and only what's written in the app is known. A type
/// from another assembly that isn't one of the kinds below is skipped and named, never guessed.
/// </summary>
static partial class ModelReader
{
    static readonly HashSet<string> Integers = ["int", "long", "short", "byte", "Int32", "Int64", "Int16", "Byte", "uint", "ulong", "ushort"];
    static readonly HashSet<string> Decimals = ["decimal", "double", "float", "Decimal", "Double", "Single"];

    /// <param name="files">The app's C# files: path from the app's folder, and text.</param>
    /// <param name="model">The class name, or a full name with its namespace.</param>
    /// <param name="nullableEnabled">Whether the project turns on nullable reference types.</param>
    public static ModelInfo Read(IReadOnlyList<(string Path, string Text)> files, string model, bool nullableEnabled)
    {
        var trees = files.Select(f => (f.Path, Root: CSharpSyntaxTree.ParseText(f.Text, path: f.Path).GetCompilationUnitRoot())).ToList();
        var simple = model.Contains('.') ? model[(model.LastIndexOf('.') + 1)..] : model;
        var wantedNamespace = model.Contains('.') ? model[..model.LastIndexOf('.')] : null;

        var declarations = trees
            .SelectMany(t => t.Root.DescendantNodes().OfType<TypeDeclarationSyntax>().Where(d => d is ClassDeclarationSyntax or RecordDeclarationSyntax).Select(d => (t.Path, Decl: d)))
            .Where(d => d.Decl.Identifier.Text == simple && d.Decl.Parent is not TypeDeclarationSyntax)
            .Where(d => wantedNamespace is null || NamespaceOf(d.Decl) == wantedNamespace)
            .ToList();
        if (declarations.Count == 0)
            throw new UsageException($"No class or record named {model} in the project. Give the model's name as it's declared, such as izzy add crud Customer.");
        var byNamespace = declarations.GroupBy(d => NamespaceOf(d.Decl)).ToList();
        if (byNamespace.Count > 1)
            throw new UsageException($"More than one {simple}: {string.Join(", ", byNamespace.Select(g => (g.Key.Length > 0 ? g.Key + "." : "") + simple))}. Give the full name.");
        var parts = byNamespace[0].ToList();
        var ns = byNamespace[0].Key;

        var enums = trees.SelectMany(t => t.Root.DescendantNodes().OfType<EnumDeclarationSyntax>())
            .GroupBy(e => e.Identifier.Text).ToDictionary(g => g.Key, g => g.First());
        var classes = trees.SelectMany(t => t.Root.DescendantNodes().OfType<TypeDeclarationSyntax>()).Select(d => d.Identifier.Text).ToHashSet();

        var fields = new List<ModelField>();
        var skipped = new List<string>();
        ModelField? key = null;
        var keyGenerated = false;
        // Its base classes in the app come first (an Entity with the Id), as their properties come first at run time.
        var declared = trees.SelectMany(t => t.Root.DescendantNodes().OfType<TypeDeclarationSyntax>()).GroupBy(d => d.Identifier.Text).ToDictionary(g => g.Key, g => g.ToList());
        var chain = new List<TypeDeclarationSyntax>();
        var seen = new HashSet<string> { simple };
        for (var level = parts.Select(p => p.Decl).ToList(); level.Count > 0;)
        {
            chain.InsertRange(0, level);
            var baseName = level.SelectMany(d => d.BaseList?.Types ?? []).Select(b => BaseName(b.Type)).FirstOrDefault(n => declared.ContainsKey(n) && declared[n].Any(d => d is ClassDeclarationSyntax or RecordDeclarationSyntax));
            level = baseName is not null && seen.Add(baseName) ? declared[baseName].Where(d => d is ClassDeclarationSyntax or RecordDeclarationSyntax).ToList() : [];
        }
        var properties = chain.SelectMany(d => d.Members.OfType<PropertyDeclarationSyntax>())
            .Where(p => p.Modifiers.Any(SyntaxKind.PublicKeyword) && !p.Modifiers.Any(SyntaxKind.StaticKeyword))
            // An override or new in the model replaces the base's.
            .Reverse().DistinctBy(p => p.Identifier.Text).Reverse()
            .ToList();
        if (parts.Any(p => HasAttribute(p.Decl.AttributeLists, "Keyless")))
            throw new UsageException($"{simple} is [Keyless], so its records have no address for a detail or edit page.");
        if (parts.Select(p => Attribute(p.Decl.AttributeLists, "PrimaryKey")).FirstOrDefault(a => a is not null) is { } primaryKey && primaryKey.ArgumentList?.Arguments.Count > 1)
            throw new UsageException($"{simple} has a key of more than one property ({primaryKey.ArgumentList}): crud's pages need one key, such as an Id.");
        // A positional record (or a class with a primary constructor) can't be made empty.
        var positional = parts.SelectMany(p => p.Decl.ParameterList?.Parameters ?? []).ToList();

        // The pages make a new one with new() and let the form fill it in, as Blazor's form binding does.
        var constructors = parts.SelectMany(p => p.Decl.Members.OfType<ConstructorDeclarationSyntax>()).Where(c => !c.Modifiers.Any(SyntaxKind.StaticKeyword)).ToList();
        if (positional.Count > 0 || constructors.Count > 0 && !constructors.Any(c => c.ParameterList.Parameters.Count == 0 && c.Modifiers.Any(SyntaxKind.PublicKeyword)))
            throw new UsageException($"{simple} has no public constructor without parameters, which crud's form needs to make one: add public {simple}() {{ }}, or give crud a class with settable properties.");
        // The edit page changes the saved one: an init-only property can't be set after it's made.
        if (properties.FirstOrDefault(p => p.AccessorList?.Accessors.Any(a => a.IsKind(SyntaxKind.InitAccessorDeclaration) && !a.Modifiers.Any()) == true) is { } initOnly)
            throw new UsageException($"{simple}.{initOnly.Identifier.Text} is init-only, and crud's edit page changes a saved {simple}: give its properties set accessors (a record can have them too).");
        if (properties.FirstOrDefault(p => p.Modifiers.Any(SyntaxKind.RequiredKeyword)) is { } requiredMember)
            throw new UsageException($"{simple}.{requiredMember.Identifier.Text} is a required member, so new {simple}() needs it set, and crud's form makes one empty and fills it in. Use [Required] instead of the required keyword.");

        string? keyName = properties.FirstOrDefault(p => HasAttribute(p.AttributeLists, "Key"))?.Identifier.Text;
        var names = properties.Select(p => p.Identifier.Text).Concat(positional.Select(p => p.Identifier.Text)).ToList();
        keyName ??= names.FirstOrDefault(n => n == "Id") ?? names.FirstOrDefault(n => n == simple + "Id");

        foreach (var p in properties)
        {
            var name = p.Identifier.Text;
            var attributes = p.AttributeLists;
            if (HasAttribute(attributes, "ScaffoldColumn") && AttributeArgument(attributes, "ScaffoldColumn") is "false")
            {
                skipped.Add($"{name}: [ScaffoldColumn(false)]");
                continue;
            }
            if (HasAttribute(attributes, "NotMapped"))
            {
                skipped.Add($"{name}: [NotMapped]");
                continue;
            }
            var setter = p.AccessorList?.Accessors.FirstOrDefault(a => a.Kind() is SyntaxKind.SetAccessorDeclaration or SyntaxKind.InitAccessorDeclaration);
            var writable = setter is not null && !setter.Modifiers.Any(m => m.Kind() is SyntaxKind.PrivateKeyword or SyntaxKind.ProtectedKeyword or SyntaxKind.InternalKeyword);
            var field = Field(name, p.Type, attributes, writable, p.Modifiers.Any(SyntaxKind.RequiredKeyword), enums, classes, NullableAt(p, nullableEnabled), out var why);
            if (field is null)
            {
                skipped.Add($"{name}: {why}");
                continue;
            }
            if (name == keyName)
            {
                key = field;
                var generated = AttributeArgument(attributes, "DatabaseGenerated");
                keyGenerated = generated is null ? field.Kind == FieldKind.Integer : !generated.EndsWith("None", StringComparison.Ordinal);
                continue;
            }
            fields.Add(field);
        }
        if (key is null)
            throw new UsageException($"{simple} has no key: add an Id property (or {simple}Id, or mark one [Key]), so each record has an address.");
        if (key.Kind is not (FieldKind.Integer or FieldKind.Guid or FieldKind.Text))
            throw new UsageException($"{simple}'s key {key.Name} is a {key.Type}: crud handles a whole number, a Guid or a string key.");
        if (fields.Count == 0)
            throw new UsageException($"{simple} has no properties crud can show besides its key.");

        var contexts = trees.SelectMany(t => t.Root.DescendantNodes().OfType<ClassDeclarationSyntax>())
            .Where(c => c.BaseList?.Types.Any(b => Regex.IsMatch(BaseName(b.Type), @"DbContext$")) == true || c.Identifier.Text.EndsWith("DbContext", StringComparison.Ordinal))
            .Select(c => (Class: c, Set: c.Members.OfType<PropertyDeclarationSyntax>().FirstOrDefault(p =>
                p.Type is GenericNameSyntax { Identifier.Text: "DbSet" } g && g.TypeArgumentList.Arguments.Count == 1 && LastName(g.TypeArgumentList.Arguments[0]) == simple)))
            .Where(c => c.Set is not null)
            .Select(c => new ContextInfo(c.Class.Identifier.Text, NamespaceOf(c.Class), c.Set!.Identifier.Text))
            .DistinctBy(c => (c.Namespace, c.Name))
            .ToList();

        return new ModelInfo(simple, ns, parts[0].Path, key, keyGenerated, fields, skipped, contexts);
    }

    static ModelField? Field(string name, TypeSyntax type, SyntaxList<AttributeListSyntax> attributes, bool writable, bool required,
        Dictionary<string, EnumDeclarationSyntax> enums, HashSet<string> classes, bool nullableEnabled, out string why)
    {
        why = "";
        var nullable = type is NullableTypeSyntax;
        var inner = type is NullableTypeSyntax n ? n.ElementType : type;
        var typeName = LastName(inner);
        var written = type.ToString();

        if (inner is GenericNameSyntax { Identifier.Text: "Nullable" } wrapped)
        {
            nullable = true;
            inner = wrapped.TypeArgumentList.Arguments[0];
            typeName = LastName(inner);
            written = inner + "?";
        }
        if (inner is ArrayTypeSyntax { ElementType: PredefinedTypeSyntax { Keyword.Text: "byte" } })
        {
            why = "binary data (a file or a row version)";
            return null;
        }
        if (inner is GenericNameSyntax or ArrayTypeSyntax)
        {
            why = "a list (related records are a Pro option)";
            return null;
        }

        var dataType = AttributeArgument(attributes, "DataType") is { } dt ? dt[(dt.LastIndexOf('.') + 1)..] : null;
        FieldKind? kind = typeName switch
        {
            "string" or "String" =>
                dataType == "MultilineText" ? FieldKind.LongText
                : dataType == "EmailAddress" || HasAttribute(attributes, "EmailAddress") || name.EndsWith("Email", StringComparison.Ordinal) ? FieldKind.Email
                : dataType == "PhoneNumber" || HasAttribute(attributes, "Phone") || name.EndsWith("Phone", StringComparison.Ordinal) ? FieldKind.Phone
                : dataType == "Url" || HasAttribute(attributes, "Url") || name.EndsWith("Url", StringComparison.Ordinal) || name == "Website" ? FieldKind.Url
                : MaxLength(attributes) > 500 ? FieldKind.LongText
                : name is "Description" or "Notes" or "Body" or "Comments" ? FieldKind.LongText
                : FieldKind.Text,
            _ when Integers.Contains(typeName) => FieldKind.Integer,
            // Money when it says so, or a decimal named like money; a double is a measurement.
            _ when Decimals.Contains(typeName) =>
                dataType == "Currency" || typeName is "decimal" or "Decimal" && Regex.IsMatch(name, "(Price|Total|Amount|Cost|Revenue|Balance|Fee|Salary|Subtotal|Tax)$") ? FieldKind.Money : FieldKind.Decimal,
            "bool" or "Boolean" => FieldKind.Bool,
            "DateOnly" => FieldKind.Date,
            "DateTime" or "DateTimeOffset" => dataType == "Date" ? FieldKind.Date : FieldKind.DateTime,
            "TimeOnly" => FieldKind.Time,
            "Guid" => FieldKind.Guid,
            _ when enums.ContainsKey(typeName) => FieldKind.Enum,
            _ => null,
        };
        if (kind is null)
        {
            why = classes.Contains(typeName) ? $"a related {typeName} (related records are a Pro option)" : $"its type {typeName} isn't one crud knows";
            return null;
        }

        var isReference = kind is FieldKind.Text or FieldKind.LongText or FieldKind.Email or FieldKind.Phone or FieldKind.Url;
        var isRequired = HasAttribute(attributes, "Required") || required
            || kind == FieldKind.Bool
            || (!nullable && (!isReference || nullableEnabled));
        var generated = AttributeArgument(attributes, "DatabaseGenerated");
        var readOnly = !writable
            || AttributeArgument(attributes, "Editable") is "false"
            || generated is not null && !generated.EndsWith("None", StringComparison.Ordinal);
        var values = kind == FieldKind.Enum
            ? enums[typeName].Members.Select(m => new EnumValue(m.Identifier.Text, DisplayName(m.AttributeLists) ?? Words(m.Identifier.Text))).ToList()
            : [];
        var label = DisplayName(attributes) ?? Words(name);
        return new ModelField(name, label, DisplayName(attributes, "ShortName") ?? label, kind.Value, written, isRequired, readOnly, MaxLength(attributes), values)
        {
            EnumNamespace = kind == FieldKind.Enum ? NamespaceOf(enums[typeName]) : "",
        };
    }

    // Whether nullable reference types are on where the property is: the last #nullable before it in its file
    // (enable, or disable; restore goes back to the project's), else the project's setting.
    static bool NullableAt(SyntaxNode node, bool project)
    {
        var directive = node.SyntaxTree.GetRoot().DescendantTrivia()
            .Where(t => t.IsKind(SyntaxKind.NullableDirectiveTrivia) && t.SpanStart < node.SpanStart)
            .Select(t => (NullableDirectiveTriviaSyntax)t.GetStructure()!)
            .LastOrDefault(d => d.TargetToken.IsKind(SyntaxKind.None) || d.TargetToken.IsKind(SyntaxKind.AnnotationsKeyword));
        return directive?.SettingToken.Kind() switch
        {
            SyntaxKind.EnableKeyword => true,
            SyntaxKind.DisableKeyword => false,
            _ => project,
        };
    }

    /// <summary>A name in words, first word capitalized: LifetimeValue is "Lifetime value", PostalCode "Postal code", URL stays.</summary>
    public static string Words(string name)
    {
        var words = Regex.Matches(name, @"[A-Z]+(?![a-z])|[A-Z]?[a-z]+|\d+").Select(m => m.Value).ToList();
        if (words.Count == 0) return name;
        return string.Join(' ', words.Select((w, i) => i == 0 || w.All(char.IsUpper) ? w : w.ToLowerInvariant()));
    }

    // [Display(Name = "...")], or with ShortName, the shorter name grids use for a column.
    static string? DisplayName(SyntaxList<AttributeListSyntax> lists, string property = "Name") =>
        Attribute(lists, "Display")?.ArgumentList?.Arguments
            .FirstOrDefault(a => a.NameEquals?.Name.Identifier.Text == property)?.Expression is LiteralExpressionSyntax { } literal
            ? literal.Token.ValueText : null;

    static int MaxLength(SyntaxList<AttributeListSyntax> lists) =>
        (AttributeArgument(lists, "StringLength") ?? AttributeArgument(lists, "MaxLength")) is { } text && int.TryParse(text, out var n) ? n : 0;

    static bool HasAttribute(SyntaxList<AttributeListSyntax> lists, string name) => Attribute(lists, name) is not null;

    static AttributeSyntax? Attribute(SyntaxList<AttributeListSyntax> lists, string name) =>
        lists.SelectMany(l => l.Attributes).FirstOrDefault(a => LastName(a.Name) is var n && (n == name || n == name + "Attribute"));

    /// <summary>The attribute's first positional argument as written, such as <c>DataType.EmailAddress</c> or <c>50</c>.</summary>
    static string? AttributeArgument(SyntaxList<AttributeListSyntax> lists, string name) =>
        Attribute(lists, name)?.ArgumentList?.Arguments.FirstOrDefault(a => a.NameEquals is null && a.NameColon is null)?.Expression.ToString();

    static string LastName(SyntaxNode node) => node switch
    {
        QualifiedNameSyntax q => q.Right.Identifier.Text,
        AliasQualifiedNameSyntax a => a.Name.Identifier.Text,
        SimpleNameSyntax s => s.Identifier.Text,
        PredefinedTypeSyntax p => p.Keyword.Text,
        NullableTypeSyntax n => LastName(n.ElementType),
        _ => node.ToString(),
    };

    static string BaseName(TypeSyntax type) => LastName(type is GenericNameSyntax g ? g : type);

    static string NamespaceOf(SyntaxNode node)
    {
        var names = new List<string>();
        for (var parent = node.Parent; parent is not null; parent = parent.Parent)
            if (parent is BaseNamespaceDeclarationSyntax ns) names.Insert(0, ns.Name.ToString());
        return string.Join('.', names);
    }
}
