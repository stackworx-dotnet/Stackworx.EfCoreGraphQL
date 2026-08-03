namespace Stackworx.EfCoreGraphQL;

using System.Diagnostics.CodeAnalysis;
using System.Collections.Generic;
using System.Linq;

internal static class TypeUtils
{
    /// <summary>
    /// The type as it is written in C#: <c>Namespace.Outer.Inner&lt;string&gt;</c>.
    /// </summary>
    public static string GetNestedQualifiedName(Type t)
    {
        var systemType = t switch
        {
            _ when t == typeof(string) => "string",
            _ when t == typeof(bool) => "bool",
            _ when t == typeof(byte) => "byte",
            _ when t == typeof(sbyte) => "sbyte",
            _ when t == typeof(short) => "short",
            _ when t == typeof(ushort) => "ushort",
            _ when t == typeof(int) => "int",
            _ when t == typeof(uint) => "uint",
            _ when t == typeof(long) => "long",
            _ when t == typeof(ulong) => "ulong",
            _ when t == typeof(float) => "float",
            _ when t == typeof(double) => "double",
            _ when t == typeof(decimal) => "decimal",
            _ when t == typeof(char) => "char",
            _ when t == typeof(object) => "object",
            _ => null,
        };

        if (systemType is not null)
        {
            return systemType;
        }

        // Builds: Namespace.Outer<TArg>.Inner (no '+', no arity suffix)
        var typeArguments = t.GetGenericArguments();
        var consumedArguments = 0;
        var parts = new List<string>();

        foreach (var level in NestingChain(t))
        {
            var part = GetNonGenericName(level);

            // A nested type's generic arguments include its declaring types', so each level owns only
            // the ones its parents left over.
            var argumentsUpToLevel = level.IsGenericType ? level.GetGenericArguments().Length : 0;
            if (argumentsUpToLevel > consumedArguments)
            {
                var owned = typeArguments[consumedArguments..argumentsUpToLevel];
                part += "<" + string.Join(", ", owned.Select(GetNestedQualifiedName)) + ">";
                consumedArguments = argumentsUpToLevel;
            }

            parts.Add(part);
        }

        var ns = t.Namespace;
        var left = ns is null ? string.Empty : ns + ".";
        return left + string.Join(".", parts);
    }

    /// <summary>
    /// The type as a legal C# identifier: <c>Revision&lt;Book&gt;</c> becomes <c>RevisionOfBook</c>.
    /// </summary>
    /// <remarks>
    /// Nesting is dropped, matching <see cref="Type.Name"/>, so two same-named types in different scopes
    /// produce the same identifier.
    /// </remarks>
    public static string GetIdentifierName(Type t)
        => t.IsGenericType
            ? GetNonGenericName(t) + "Of" + string.Join("And", t.GetGenericArguments().Select(GetIdentifierName))
            : GetNonGenericName(t);

    private static IEnumerable<Type> NestingChain(Type t)
    {
        var parts = new Stack<Type>();
        var cur = t;
        while (cur is not null)
        {
            parts.Push(cur);
            cur = cur.DeclaringType;
        }

        return parts;
    }

    private static string GetNonGenericName(Type t)
    {
        var name = t.Name;
        var backtick = name.IndexOf('`');
        return backtick >= 0 ? name[..backtick] : name;
    }

    public static bool TryUnwrapNullable(
        Type type,
        [NotNullWhen(returnValue: true)]
        out Type? innerType)
    {
        var underlying = Nullable.GetUnderlyingType(type);
        if (underlying != null)
        {
            innerType = underlying;
            return true;
        }

        innerType = type;
        return false;
    }

    public static bool TryUnwrapCollectionType(Type clrType, out Type elementType)
    {
        // 1️⃣ Handle array
        if (clrType.IsArray)
        {
            elementType = clrType.GetElementType()!;
            return true;
        }

        // 2️⃣ Handle generic IEnumerable<T> / ICollection<T> / IList<T> / HashSet<T> etc.
        if (clrType.IsGenericType)
        {
            // If the type itself is IEnumerable<T> (or similar), unwrap directly.
            if (clrType.GetGenericTypeDefinition() == typeof(IEnumerable<>))
            {
                elementType = clrType.GetGenericArguments()[0];
                return true;
            }

            // Otherwise, look for any implemented interface that is IEnumerable<T>
            var enumerableIface = clrType
                .GetInterfaces()
                .FirstOrDefault(i =>
                    i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>));

            if (enumerableIface is not null)
            {
                elementType = enumerableIface.GetGenericArguments()[0];
                return true;
            }
        }

        elementType = clrType;
        return false;
    }

    public static string ToGraphQlFieldName(string name)
        => string.IsNullOrEmpty(name) || char.IsLower(name[0])
            ? name
            : char.ToLowerInvariant(name[0]) + name.Substring(1);
}