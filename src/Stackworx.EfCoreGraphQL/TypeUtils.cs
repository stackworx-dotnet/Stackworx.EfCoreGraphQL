namespace Stackworx.EfCoreGraphQL;

using System.Diagnostics.CodeAnalysis;
using System.Collections.Generic;
using System.Linq;

internal static class TypeUtils
{
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

        // Builds: Namespace.Outer.Inner (no '+', strips arity)
        var parts = new Stack<string>();
        var cur = t;
        while (cur is not null)
        {
            parts.Push(GetNonGenericName(cur));
            cur = cur.DeclaringType;
        }

        var ns = t.Namespace;
        var left = ns is null ? string.Empty : ns + ".";
        return left + string.Join(".", parts);
    }

    private static string GetNonGenericName(Type t)
    {
        var name = t.Name;
        var backtick = name.IndexOf('`');
        return backtick >= 0 ? name[..backtick] : name;
    }

    public static string CsDisplay(Type t)
        => t.FullName switch
        {
            "System.Int32" => "int",
            "System.Int64" => "long",
            "System.Guid" => "Guid",
            "System.String" => "string",
            "System.Boolean" => "bool",
            "System.DateTime" => "DateTime",
            "System.DateTimeOffset" => "DateTimeOffset",
            _ => t.FullName ?? t.Name
        };

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
}