using System.Reflection;

namespace IzzyBlazor.DocsGen;

/// <summary>Writes types the way C# source does: <c>string?</c>, <c>EventCallback&lt;MouseEventArgs&gt;</c>.</summary>
public static class TypeNames
{
    private static readonly Dictionary<Type, string> Aliases = new()
    {
        [typeof(string)] = "string", [typeof(bool)] = "bool", [typeof(int)] = "int", [typeof(long)] = "long",
        [typeof(short)] = "short", [typeof(byte)] = "byte", [typeof(double)] = "double", [typeof(float)] = "float",
        [typeof(decimal)] = "decimal", [typeof(object)] = "object", [typeof(char)] = "char", [typeof(uint)] = "uint",
        [typeof(ulong)] = "ulong", [typeof(void)] = "void",
    };

    private static readonly NullabilityInfoContext Nullability = new();

    public static string Of(PropertyInfo property) => Of(Nullability.Create(property));

    public static string Of(ParameterInfo parameter) => Of(Nullability.Create(parameter));

    private static string Of(NullabilityInfo info)
    {
        var type = info.Type;
        if (Nullable.GetUnderlyingType(type) is { } underlying)
        {
            return Of(underlying) + "?";
        }

        string name;
        if (type.IsArray && info.ElementType is not null)
        {
            name = Of(info.ElementType) + "[]";
        }
        else if (type.IsGenericType && info.GenericTypeArguments.Length > 0)
        {
            name = $"{BaseName(type)}<{string.Join(", ", info.GenericTypeArguments.Select(Of))}>";
        }
        else
        {
            name = Of(type);
        }

        // Unconstrained type parameters read as nullable whatever the source says; leave them bare.
        return !type.IsValueType && !type.IsGenericParameter && info.ReadState == NullabilityState.Nullable ? name + "?" : name;
    }

    /// <summary>A type's name without nullability annotations.</summary>
    public static string Of(Type type)
    {
        if (Aliases.TryGetValue(type, out var alias))
        {
            return alias;
        }

        if (Nullable.GetUnderlyingType(type) is { } underlying)
        {
            return Of(underlying) + "?";
        }

        if (type.IsArray)
        {
            return Of(type.GetElementType()!) + "[]";
        }

        if (type.IsGenericType)
        {
            return $"{BaseName(type)}<{string.Join(", ", type.GetGenericArguments().Select(Of))}>";
        }

        return type.Name;
    }

    private static string BaseName(Type type)
    {
        var name = type.Name;
        var tick = name.IndexOf('`');
        return tick < 0 ? name : name[..tick];
    }

    /// <summary>A component's name as written in Razor: <c>Select&lt;TValue&gt;</c>.</summary>
    public static string Display(Type type) =>
        type.IsGenericTypeDefinition ? $"{BaseName(type)}<{string.Join(", ", type.GetGenericArguments().Select(a => a.Name))}>" : type.Name;

    /// <summary>The name without generic arity: <c>Select</c>.</summary>
    public static string Plain(Type type) => BaseName(type);
}
