using System.Globalization;
using System.Linq.Expressions;
using System.Net;
using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Components;

namespace IzzyBlazor.DocsGen;

/// <summary>
/// Reads components from the library's assemblies and their XML docs. A component is a public,
/// non-abstract <see cref="IComponent"/>; its API is its <c>[Parameter]</c> properties (minus the
/// captured-attributes bag), with <c>EventCallback</c>s as events and public methods of its own.
/// Anything marked <c>[Obsolete]</c> as an error is left out: it's gone from the API, kept only so
/// old markup fails to build (Row's Direction). A deprecation that still works (a warning) is listed
/// with its message; the site leaves those off its pages.
/// </summary>
public sealed partial class ApiReader(XmlDocs docs, string version, RazorHeaders? headers = null)
{
    /// <summary>Bases whose props many components share; they're listed once, not on each component.</summary>
    public static readonly string[] SharedBases = ["IzzyComponentBase", "LayoutItemBase", "StyledComponentBase", "FieldBase"];

    private const BindingFlags Instance = BindingFlags.Public | BindingFlags.Instance;

    // Members Blazor's own InputBase declares; its XML docs aren't part of the library's.
    private static readonly Dictionary<string, string> FrameworkSummaries = new()
    {
        ["Value"] = "<p>The value, bound with <code>@bind-Value</code>.</p>",
        ["DisplayName"] = "<p>The field's name in validation messages from the input itself, such as a value that can't be parsed.</p>",
    };

    private readonly HashSet<Type> _usedTypes = [];
    private readonly Dictionary<Type, object?> _instances = [];

    public ApiFile Read(IEnumerable<Assembly> assemblies)
    {
        var list = assemblies.ToList();
        var components = list
            .SelectMany(a => a.GetExportedTypes().Select(t => (Assembly: a, Type: t)))
            .Where(x => IsComponent(x.Type))
            .OrderBy(x => x.Type.Name, StringComparer.Ordinal)
            .Select(x => ReadComponent(x.Type, x.Assembly))
            .ToList();

        var componentTypes = list.SelectMany(a => a.GetExportedTypes()).Where(IsComponent).ToList();
        var bases = list
            .SelectMany(a => a.GetExportedTypes())
            .Where(t => SharedBases.Contains(TypeNames.Plain(t)))
            .OrderBy(t => Array.IndexOf(SharedBases, TypeNames.Plain(t)))
            .Select(t => ReadBase(t, componentTypes))
            .ToList();

        var types = _usedTypes
            .Where(t => list.Contains(t.Assembly) && !IsComponent(t) && !IsHidden(t))
            .OrderBy(t => t.Name, StringComparer.Ordinal)
            .Select(ReadType)
            .ToList();

        return new ApiFile { Version = version, Components = components, Bases = bases, Types = types };
    }

    public static bool IsComponent(Type type) =>
        type is { IsPublic: true, IsAbstract: false, IsInterface: false } &&
        typeof(IComponent).IsAssignableFrom(type) &&
        type.Namespace == "IzzyBlazor" &&
        !Removed(type);

    /// <summary>Marked <c>[Obsolete]</c> as an error: not part of the API.</summary>
    private static bool Removed(MemberInfo member) => member.GetCustomAttribute<ObsoleteAttribute>() is { IsError: true };

    // Types not worth a section: the icon catalogs (thousands of icons) and Responsive<T>, which
    // the Common features docs cover once.
    private static bool IsHidden(Type type) => type.Name is "Lucide" or "Brands" or "Responsive`1" or "IconData";

    private ApiComponent ReadComponent(Type type, Assembly assembly)
    {
        var doc = docs.ForType(type);
        var header = headers?.For(TypeNames.Plain(type));
        var instance = InstanceOf(type);
        var parameters = Parameters(type).ToList();
        var names = parameters.Select(p => p.Name).ToHashSet();

        // Inherited parameters it refuses (Heading's As) aren't on its pages.
        var refused = type.GetCustomAttributes<RefusesParameterAttribute>(inherit: false).Select(a => a.Name).ToHashSet();
        if (refused.FirstOrDefault(name => parameters.FirstOrDefault(p => p.Name == name) is not { } p || p.DeclaringType == type || SharedBases.Contains(TypeNames.Plain(p.DeclaringType!))) is { } wrong)
        {
            throw new InvalidOperationException($"{type.Name} refuses {wrong}, which isn't a parameter it inherits from a base of its own (shared bases aren't supported).");
        }

        parameters.RemoveAll(p => refused.Contains(p.Name));
        names.ExceptWith(refused);

        var properties = new List<ApiMember>();
        var events = new List<ApiMember>();
        foreach (var property in parameters)
        {
            var declaring = TypeNames.Plain(property.DeclaringType!);
            if (SharedBases.Contains(declaring))
            {
                continue;
            }

            // @bind pairs: Value plus ValueChanged (and the compiler's ValueExpression) read as one.
            if (property.Name.EndsWith("Changed", StringComparison.Ordinal) && names.Contains(property.Name[..^"Changed".Length]))
            {
                continue;
            }

            if (property.Name.EndsWith("Expression", StringComparison.Ordinal) && names.Contains(property.Name[..^"Expression".Length]) &&
                typeof(LambdaExpression).IsAssignableFrom(property.PropertyType))
            {
                continue;
            }

            var changed = parameters.FirstOrDefault(p => p.Name == property.Name + "Changed");
            var member = ReadMember(property, instance, type, changed);
            (IsEvent(property.PropertyType) ? events : properties).Add(member);
        }

        var methods = type.GetMethods(Instance)
            .Where(m => IsOwnMethod(m, type))
            .OrderBy(m => m.Name, StringComparer.Ordinal)
            .Select(ReadMethod)
            .ToList();

        var componentBases = Ancestors(type).Select(TypeNames.Plain).Where(SharedBases.Contains).ToList();
        var memberTypes = properties.Concat(events).Concat(methods).ToList();

        return new ApiComponent
        {
            Name = TypeNames.Plain(type),
            DisplayName = TypeNames.Display(type),
            FullName = XmlDocs.DocName(type),
            Package = assembly.GetName().Name!,
            Tier = assembly.GetName().Name!.EndsWith(".Pro", StringComparison.Ordinal) ? "Pro" : "Free",
            // A .razor component has no XML doc comment: its header comment stands in.
            Summary = XmlDocs.Html(doc, "summary") ?? header?.Summary,
            Remarks = XmlDocs.Html(doc, "remarks") ?? header?.Remarks,
            Examples = XmlDocs.Examples(doc) ?? (header?.Example is { } example ? [example] : null),
            Accessibility = XmlDocs.Html(doc, "accessibility") ?? header?.Accessibility,
            SeeAlso = XmlDocs.SeeAlso(doc) ?? header?.SeeAlso,
            TypeParameters = type.IsGenericTypeDefinition ? type.GetGenericArguments().Select(a => a.Name).ToList() : null,
            Bases = componentBases,
            // The attribute isn't inherited: DialogClose derives from Button, but Button's modes aren't its.
            Modes = Modes(type.GetCustomAttribute<ModeSupportAttribute>(inherit: false)),
            Properties = properties,
            Events = events,
            Methods = methods,
            Types = TypesOf(type, parameters, methods.Count > 0 ? type.GetMethods(Instance).Where(m => IsOwnMethod(m, type)) : []),
            Obsolete = ObsoleteText(type),
        };
    }

    private List<string> TypesOf(Type component, IEnumerable<PropertyInfo> parameters, IEnumerable<MethodInfo> methods)
    {
        var found = new HashSet<Type>();
        // Not the types of deprecated members: they're on their way out.
        foreach (var property in parameters.Where(p => !SharedBases.Contains(TypeNames.Plain(p.DeclaringType!)) && ObsoleteText(p) is null))
        {
            Collect(property.PropertyType, found);
        }

        foreach (var method in methods.Where(m => ObsoleteText(m) is null))
        {
            Collect(method.ReturnType, found);
            foreach (var parameter in method.GetParameters())
            {
                Collect(parameter.ParameterType, found);
            }
        }

        found.RemoveWhere(t => t.Namespace != "IzzyBlazor" || IsHidden(t) || t == component || IsComponent(t));
        _usedTypes.UnionWith(found);
        return found.Select(t => TypeNames.Plain(t)).Distinct().OrderBy(n => n, StringComparer.Ordinal).ToList();
    }

    private static void Collect(Type type, HashSet<Type> found)
    {
        if (type.IsGenericParameter)
        {
            return;
        }

        if (type.IsArray || type.IsByRef)
        {
            Collect(type.GetElementType()!, found);
            return;
        }

        if (type.IsGenericType)
        {
            foreach (var argument in type.GetGenericArguments())
            {
                Collect(argument, found);
            }

            type = type.GetGenericTypeDefinition();
        }

        found.Add(type);
    }

    private ApiBase ReadBase(Type type, IReadOnlyList<Type> components)
    {
        var properties = type.GetProperties(Instance | BindingFlags.DeclaredOnly).Where(IsParameter).ToList();
        var found = new HashSet<Type>();
        foreach (var property in properties.Where(p => ObsoleteText(p) is null))
        {
            Collect(property.PropertyType, found);
        }

        found.RemoveWhere(t => t.Namespace != "IzzyBlazor" || IsHidden(t) || IsComponent(t));
        _usedTypes.UnionWith(found);

        var takers = components.Where(c => Ancestors(c).Any(a => a == type)).ToList();
        return new ApiBase
        {
            Name = TypeNames.Plain(type),
            Summary = XmlDocs.Html(docs.ForType(type), "summary"),
            Properties = properties.Select(p => ReadMember(p, null, type, changed: null) with { Default = SharedDefault(p, takers) }).ToList(),
        };
    }

    // A shared prop's default, from every component that takes it as declared on the base (not
    // one that redeclares it): the value they all start with, or none when they differ.
    private string? SharedDefault(PropertyInfo property, IEnumerable<Type> takers)
    {
        var summary = XmlDocs.Html(docs.ForProperty(property), "summary");
        var values = takers
            .Where(c => MostDerived(c, property.Name)?.DeclaringType is { } declaring && Generic(declaring) == Generic(property.DeclaringType!))
            .Select(InstanceOf)
            .OfType<object>()
            .Select(i => DefaultOf(property, i, summary))
            .Distinct()
            .ToList();
        return values.Count == 1 ? values[0] : null;
    }

    private static Type Generic(Type type) => type.IsGenericType ? type.GetGenericTypeDefinition() : type;

    /// <summary>A parameter; with its <paramref name="changed"/> callback, one bindable member.</summary>
    private ApiMember ReadMember(PropertyInfo property, object? instance, Type component, PropertyInfo? changed)
    {
        var doc = docs.ForProperty(property);
        var summary = XmlDocs.Html(doc, "summary");
        if (summary is null && property.DeclaringType!.Namespace?.StartsWith("Microsoft.", StringComparison.Ordinal) == true)
        {
            summary = FrameworkSummaries.GetValueOrDefault(property.Name);
        }

        var declaring = TypeNames.Plain(property.DeclaringType!);
        return new ApiMember
        {
            Name = property.Name,
            Type = TypeNames.Of(property),
            Default = DefaultOf(property, instance, summary),
            Summary = summary,
            Remarks = XmlDocs.Html(doc, "remarks"),
            Bindable = changed is not null,
            BindModes = changed is null ? null : Modes(changed.GetCustomAttribute<ModeSupportAttribute>()),
            Required = property.GetCustomAttribute<EditorRequiredAttribute>() is not null,
            Responsive = property.PropertyType.IsGenericType && property.PropertyType.GetGenericTypeDefinition().Name == "Responsive`1",
            Modes = Modes(property.GetCustomAttribute<ModeSupportAttribute>()),
            Obsolete = ObsoleteText(property),
            DeclaredOn = declaring != TypeNames.Plain(component) ? TypeNames.Display(property.DeclaringType!.IsGenericType ? property.DeclaringType.GetGenericTypeDefinition() : property.DeclaringType) : null,
        };
    }

    private ApiMember ReadMethod(MethodInfo method)
    {
        var doc = docs.ForMethod(method);
        var parameters = string.Join(", ", method.GetParameters().Select(p => $"{TypeNames.Of(p)} {p.Name}"));
        var generic = method.IsGenericMethodDefinition ? $"<{string.Join(", ", method.GetGenericArguments().Select(a => a.Name))}>" : "";
        return new ApiMember
        {
            Name = method.Name,
            Type = $"{TypeNames.Of(method.ReturnParameter)} {method.Name}{generic}({parameters})",
            Summary = XmlDocs.Html(doc, "summary"),
            Remarks = XmlDocs.Html(doc, "remarks"),
            Modes = Modes(method.GetCustomAttribute<ModeSupportAttribute>()),
            Obsolete = ObsoleteText(method),
        };
    }

    /// <summary>The <c>[Obsolete]</c> message, "Obsolete." when it has none, or null when it isn't obsolete.</summary>
    private static string? ObsoleteText(MemberInfo member) =>
        member.GetCustomAttribute<ObsoleteAttribute>() is { } obsolete ? obsolete.Message ?? "Obsolete." : null;

    private ApiType ReadType(Type type)
    {
        var doc = docs.ForType(type);
        var kind = type.IsEnum ? "enum"
            : type.IsInterface ? "interface"
            : typeof(Delegate).IsAssignableFrom(type) ? "delegate"
            : type.GetMethod("<Clone>$") is not null ? "record"
            : type.IsValueType ? "struct"
            : "class";

        List<ApiMember> members;
        if (type.IsEnum)
        {
            members = type.GetFields(BindingFlags.Public | BindingFlags.Static)
                .Where(f => !Removed(f))
                .Select(f => new ApiMember { Name = f.Name, Type = TypeNames.Plain(type), Summary = XmlDocs.Html(docs.ForField(f), "summary"), Obsolete = ObsoleteText(f) })
                .ToList();
        }
        else
        {
            // Presets and factories: static members that give the type itself (Accent.Primary, Accent.Of).
            var statics = type.IsGenericTypeDefinition ? [] : type.GetProperties(BindingFlags.Public | BindingFlags.Static)
                .Where(p => p.PropertyType == type)
                .Select(p => new ApiMember { Name = p.Name, Type = TypeNames.Of(p), Summary = XmlDocs.Html(docs.ForProperty(p), "summary") });
            var factories = type.IsGenericTypeDefinition ? [] : type.GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Where(m => m.ReturnType == type && !m.IsSpecialName)
                .Select(ReadMethod);

            // Then what an instance has: an options class's settings (ConfirmOptions.Accent), a
            // record's values and helpers (DataRequest.Page, ApplySorting).
            var instance = InstanceOf(type);
            var properties = type.GetProperties(Instance)
                .Where(p => p.GetIndexParameters().Length == 0 && p.GetMethod is { IsPublic: true } && !Removed(p))
                .OrderBy(p => p.MetadataToken)
                .Select(p => ReadTypeProperty(p, doc, instance));
            var methods = type.GetMethods(Instance)
                .Where(m => !m.IsSpecialName && m.DeclaringType == type && !ObjectMethods.Contains(m.Name) && !Removed(m))
                .OrderBy(m => m.Name, StringComparer.Ordinal)
                .Select(ReadMethod);
            members = statics.Concat(factories).Concat(properties).Concat(methods).ToList();
        }

        return new ApiType
        {
            Name = TypeNames.Plain(type),
            FullName = XmlDocs.DocName(type),
            Kind = kind,
            Summary = XmlDocs.Html(doc, "summary"),
            Members = members,
            Obsolete = ObsoleteText(type),
        };
    }

    // What every class or record has, which says nothing about this one.
    private static readonly HashSet<string> ObjectMethods = ["Equals", "GetHashCode", "ToString", "Deconstruct", "PrintMembers", "<Clone>$", "GetType"];

    // A property of a type: a positional record's are documented by the type's <param> tags.
    private ApiMember ReadTypeProperty(PropertyInfo property, System.Xml.Linq.XElement? typeDoc, object? instance)
    {
        var summary = XmlDocs.Html(docs.ForProperty(property), "summary") ?? XmlDocs.Param(typeDoc, property.Name);
        return new ApiMember
        {
            Name = property.Name,
            Type = TypeNames.Of(property),
            Default = DefaultOf(property, instance, summary),
            Summary = summary,
            Remarks = XmlDocs.Html(docs.ForProperty(property), "remarks"),
            Modes = Modes(property.GetCustomAttribute<ModeSupportAttribute>()),
            Obsolete = ObsoleteText(property),
        };
    }

    private static ApiModes? Modes(ModeSupportAttribute? attribute) => attribute is null ? null : new ApiModes
    {
        Off = Level(attribute.Off),
        Static = Level(attribute.Static),
        Live = Level(attribute.Live),
        Notes = attribute.Notes,
    };

    private static string Level(ModeLevel level) => level.ToString().ToLowerInvariant();

    /// <summary>The component's parameters, most-derived first when a property is redeclared.</summary>
    private static IEnumerable<PropertyInfo> Parameters(Type type)
    {
        var seen = new HashSet<string>();
        for (var current = type; current is not null && current != typeof(object); current = current.BaseType)
        {
            foreach (var property in current.GetProperties(Instance | BindingFlags.DeclaredOnly).Where(IsParameter))
            {
                if (seen.Add(property.Name))
                {
                    yield return property;
                }
            }
        }
    }

    private static bool IsParameter(PropertyInfo property) =>
        property.GetCustomAttribute<ParameterAttribute>() is { CaptureUnmatchedValues: false } && !Removed(property);

    private static bool IsEvent(Type type) =>
        type == typeof(EventCallback) || (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(EventCallback<>));

    private static readonly HashSet<string> LifecycleMethods =
        ["SetParametersAsync", "Dispose", "DisposeAsync", "Attach", "HandleEventAsync", "OnAfterRenderAsync"];

    private static bool IsOwnMethod(MethodInfo method, Type component) =>
        !method.IsSpecialName &&
        method.DeclaringType is { } declaring &&
        declaring.Namespace == "IzzyBlazor" &&
        !SharedBases.Contains(TypeNames.Plain(declaring)) &&
        declaring.IsAssignableFrom(component) &&
        method.GetBaseDefinition().DeclaringType is { Namespace: "IzzyBlazor" } &&
        !LifecycleMethods.Contains(method.Name) &&
        !Removed(method);

    private static IEnumerable<Type> Ancestors(Type type)
    {
        for (var current = type.BaseType; current is not null && current != typeof(object); current = current.BaseType)
        {
            yield return current.IsGenericType ? current.GetGenericTypeDefinition() : current;
        }
    }

    private object? InstanceOf(Type type)
    {
        if (!_instances.TryGetValue(type, out var instance))
        {
            _instances[type] = instance = Instantiate(type);
        }

        return instance;
    }

    // Defaults: read from a fresh instance. Generic components are made with a common argument.
    private static object? Instantiate(Type type)
    {
        foreach (var candidate in Candidates(type))
        {
            try
            {
                return Activator.CreateInstance(candidate);
            }
            catch (Exception)
            {
                // Constraints or a constructor that needs services: no defaults for this one.
            }
        }

        return null;
    }

    private static IEnumerable<Type> Candidates(Type type)
    {
        if (!type.IsGenericTypeDefinition)
        {
            yield return type;
            yield break;
        }

        var count = type.GetGenericArguments().Length;
        foreach (var argument in new[] { typeof(string), typeof(int), typeof(object) })
        {
            Type? made = null;
            try
            {
                made = type.MakeGenericType(Enumerable.Repeat(argument, count).ToArray());
            }
            catch (ArgumentException)
            {
                // Doesn't satisfy a constraint.
            }

            if (made is not null)
            {
                yield return made;
            }
        }
    }

    private static string? DefaultOf(PropertyInfo property, object? instance, string? summary)
    {
        object? value = null;
        var known = false;
        if (instance is not null)
        {
            try
            {
                // Most-derived first: GetProperty throws on a property hidden with "new".
                var actual = MostDerived(instance.GetType(), property.Name) ?? property;
                value = actual.GetValue(instance);
                known = true;
            }
            catch (Exception)
            {
                known = false;
            }
        }

        if (!known)
        {
            return null;
        }

        var formatted = Format(value, property.PropertyType);
        if (formatted is null && summary is not null && StatedDefault().Match(summary) is { Success: true } stated)
        {
            // Null on a fresh instance, filled in at render time: a constant when the docs name one
            // ("Close", Color.Surface), else "computed" (the text color, the shell's position).
            return Literal(stated.Groups[1].Value) ?? "computed";
        }

        return formatted;
    }

    /// <summary>"Defaults to X." in a summary, when X is one value: a quoted string, a number, code or a link to an enum value.</summary>
    public static string? Literal(string html)
    {
        if (QuotedLiteral().Match(html) is { Success: true } quoted)
        {
            return $"\"{WebUtility.HtmlDecode(quoted.Groups[1].Value)}\"";
        }

        if (CodeLiteral().Match(html) is { Success: true } code)
        {
            return WebUtility.HtmlDecode(code.Groups[1].Value);
        }

        return NumberLiteral().IsMatch(html) ? html : null;
    }

    [GeneratedRegex(@"Defaults to (.+?)\.(?:</p>|\s|$)")]
    private static partial Regex StatedDefault();

    [GeneratedRegex("^&quot;([^&]*)&quot;$")]
    private static partial Regex QuotedLiteral();

    // <code>Color.Surface</code>, or a link to an enum value: <a href="cref:F:..."><code>AvatarSize.Md</code></a>.
    [GeneratedRegex("^(?:<a href=\"cref:F:[^\"]+\">)?<code>([^<]+)</code>(?:</a>)?$")]
    private static partial Regex CodeLiteral();

    [GeneratedRegex(@"^-?\d+(\.\d+)?$")]
    private static partial Regex NumberLiteral();

    private static PropertyInfo? MostDerived(Type type, string name)
    {
        for (var current = type; current is not null; current = current.BaseType)
        {
            if (current.GetProperty(name, Instance | BindingFlags.DeclaredOnly) is { } found)
            {
                return found;
            }
        }

        return null;
    }

    /// <summary>A value as C# would write it, or null when there's nothing worth showing.</summary>
    public static string? Format(object? value, Type declared)
    {
        switch (value)
        {
            case null:
                return null;
            case bool b:
                return b ? "true" : "false";
            case string s:
                return $"\"{s}\"";
            case Enum e:
                return string.Join(" | ", e.ToString().Split(", ").Select(v => $"{e.GetType().Name}.{v}"));
            case int or long or short or byte or double or float or decimal:
                return Convert.ToString(value, CultureInfo.InvariantCulture);
            case TimeSpan t:
                return $"TimeSpan.FromMilliseconds({t.TotalMilliseconds.ToString(CultureInfo.InvariantCulture)})";
        }

        var type = value.GetType();

        // A preset of its own type: Accent.Primary.
        foreach (var candidate in new[] { type, Nullable.GetUnderlyingType(declared) ?? declared }.Distinct())
        {
            foreach (var preset in candidate.GetProperties(BindingFlags.Public | BindingFlags.Static).Where(p => p.PropertyType == candidate && p.GetIndexParameters().Length == 0))
            {
                object? presetValue;
                try
                {
                    presetValue = preset.GetValue(null);
                }
                catch (Exception)
                {
                    continue;
                }

                if (Equals(presetValue, value))
                {
                    return $"{candidate.Name}.{preset.Name}";
                }
            }
        }

        // A struct at its default (an unset Responsive<T>, a zero Length) says nothing.
        if (type.IsValueType && Equals(value, Activator.CreateInstance(type)))
        {
            return null;
        }

        // Collections and delegates: nothing useful to show.
        if (value is System.Collections.IEnumerable or Delegate)
        {
            return null;
        }

        var text = value.ToString();
        return text is null || text == type.FullName || text == type.Name ? null : text;
    }
}
