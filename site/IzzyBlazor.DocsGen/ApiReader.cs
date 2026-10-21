using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;
using Microsoft.AspNetCore.Components;

namespace IzzyBlazor.DocsGen;

/// <summary>
/// Reads components from the library's assemblies and their XML docs. A component is a public,
/// non-abstract <see cref="IComponent"/>; its API is its <c>[Parameter]</c> properties (minus the
/// captured-attributes bag), with <c>EventCallback</c>s as events and public methods of its own.
/// </summary>
public sealed class ApiReader(XmlDocs docs, string version, RazorHeaders? headers = null)
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
        type.GetCustomAttribute<ObsoleteAttribute>() is not { IsError: true };

    // Types not worth a section: the icon catalogs (thousands of icons) and Responsive<T>, which
    // the Common features docs cover once.
    private static bool IsHidden(Type type) => type.Name is "Lucide" or "Brands" or "Responsive`1" or "IconData";

    private ApiComponent ReadComponent(Type type, Assembly assembly)
    {
        var doc = docs.ForType(type);
        var instance = Instantiate(type);
        var parameters = Parameters(type).ToList();
        var names = parameters.Select(p => p.Name).ToHashSet();

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
            Summary = XmlDocs.Html(doc, "summary") ?? headers?.For(TypeNames.Plain(type)).Summary,
            Remarks = XmlDocs.Html(doc, "remarks"),
            Examples = XmlDocs.Examples(doc) ?? (headers?.For(TypeNames.Plain(type)).Example is { } example ? [example] : null),
            TypeParameters = type.IsGenericTypeDefinition ? type.GetGenericArguments().Select(a => a.Name).ToList() : null,
            Bases = componentBases,
            // Not inherited: DialogClose derives from Button, but Button's modes aren't its.
            Modes = Modes(type.GetCustomAttribute<ModeSupportAttribute>(inherit: false)),
            Properties = properties,
            Events = events,
            Methods = methods,
            Types = TypesOf(type, parameters, methods.Count > 0 ? type.GetMethods(Instance).Where(m => IsOwnMethod(m, type)) : []),
        };
    }

    private List<string> TypesOf(Type component, IEnumerable<PropertyInfo> parameters, IEnumerable<MethodInfo> methods)
    {
        var found = new HashSet<Type>();
        foreach (var property in parameters.Where(p => !SharedBases.Contains(TypeNames.Plain(p.DeclaringType!))))
        {
            Collect(property.PropertyType, found);
        }

        foreach (var method in methods)
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
        foreach (var property in properties)
        {
            Collect(property.PropertyType, found);
        }

        found.RemoveWhere(t => t.Namespace != "IzzyBlazor" || IsHidden(t) || IsComponent(t));
        _usedTypes.UnionWith(found);

        // Defaults come from a component that takes these props without redeclaring them.
        var sample = components
            .Where(c => Ancestors(c).Any(a => a == type) && properties.All(p => c.GetProperties(Instance).Count(x => x.Name == p.Name) == 1))
            .Select(Instantiate)
            .FirstOrDefault(i => i is not null);

        return new ApiBase
        {
            Name = TypeNames.Plain(type),
            Summary = XmlDocs.Html(docs.ForType(type), "summary"),
            Properties = properties.Select(p => ReadMember(p, sample, type, changed: null)).ToList(),
        };
    }

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
            Obsolete = property.GetCustomAttribute<ObsoleteAttribute>()?.Message,
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
        };
    }

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
                .Where(f => f.GetCustomAttribute<ObsoleteAttribute>() is not { IsError: true })
                .Select(f => new ApiMember { Name = f.Name, Type = TypeNames.Plain(type), Summary = XmlDocs.Html(docs.ForField(f), "summary") })
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
            members = statics.Concat(factories).ToList();
        }

        return new ApiType
        {
            Name = TypeNames.Plain(type),
            FullName = XmlDocs.DocName(type),
            Kind = kind,
            Summary = XmlDocs.Html(doc, "summary"),
            Members = members,
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
        property.GetCustomAttribute<ParameterAttribute>() is { CaptureUnmatchedValues: false } &&
        property.GetCustomAttribute<ObsoleteAttribute>() is not { IsError: true };

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
        method.GetCustomAttribute<ObsoleteAttribute>() is not { IsError: true };

    private static IEnumerable<Type> Ancestors(Type type)
    {
        for (var current = type.BaseType; current is not null && current != typeof(object); current = current.BaseType)
        {
            yield return current.IsGenericType ? current.GetGenericTypeDefinition() : current;
        }
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
        if (formatted is null && summary?.Contains("Defaults to", StringComparison.Ordinal) == true)
        {
            return "computed";
        }

        return formatted;
    }

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
