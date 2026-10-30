using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using HL7Workbench.Scripting;

namespace HL7Workbench.Workspace;

/// <summary>Exact declared facade contract, derived from the same assembly referenced by ScriptHost.</summary>
public sealed record ScriptApiContract(string Member, string DeclaringType, string Name, string Kind,
    string Signature, string ReturnType, bool IsStatic, IReadOnlyList<ScriptApiParameter> Parameters);

/// <summary>CLR parameter metadata rendered as a C# authoring contract, including nullable and optional values.</summary>
public sealed record ScriptApiParameter(string Name, string Type, string Modifier, bool Optional, string? DefaultValue);

internal static class ScriptApiContracts
{
    private const BindingFlags Flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
    internal static ScriptApiContract[] Read(IReadOnlyList<string> supported)
    {
        var nullability = new NullabilityInfoContext();
        var contracts = new List<ScriptApiContract>();
        foreach (var name in supported)
        {
            var type = typeof(ScriptGlobals).Assembly.GetType("HL7Workbench.Scripting." + name)
                ?? throw new InvalidDataException("Reference names an unknown runtime type.");
            foreach (var member in type.GetMembers(Flags).Where(m => m is PropertyInfo or FieldInfo or ConstructorInfo ||
                m is MethodInfo method && !method.IsSpecialName && !method.IsDefined(typeof(CompilerGeneratedAttribute))))
            {
                if (member is FieldInfo { Name: "value__" }) continue;
                var key = name + "." + (member is ConstructorInfo ? ".ctor" : member.Name);
                string signature, result, kind; bool isStatic; ScriptApiParameter[] parameters = [];
                if (member is MethodBase callable)
                {
                    parameters = callable.GetParameters().Select(p => new ScriptApiParameter(p.Name!,
                        TypeName(p.ParameterType, nullability.Create(p)), p.IsOut ? "out" : p.ParameterType.IsByRef ? "ref" :
                        p.IsDefined(typeof(ParamArrayAttribute)) ? "params" : "", p.IsOptional,
                        p.IsOptional ? Literal(p.DefaultValue, p.ParameterType) : null)).ToArray();
                    result = callable is MethodInfo method ? TypeName(method.ReturnType, nullability.Create(method.ReturnParameter)) : name;
                    kind = callable is ConstructorInfo ? "Constructor" : "Method"; isStatic = callable.IsStatic;
                    signature = (isStatic ? "static " : "") + (callable is ConstructorInfo ? name : result + " " + name + "." + member.Name) +
                        "(" + string.Join(", ", parameters.Select(p => (p.Modifier.Length > 0 ? p.Modifier + " " : "") + p.Type + " " + p.Name +
                            (p.Optional ? " = " + p.DefaultValue : ""))) + ")";
                }
                else if (member is PropertyInfo property)
                {
                    result = TypeName(property.PropertyType, nullability.Create(property)); kind = "Property";
                    isStatic = (property.GetMethod ?? property.SetMethod)!.IsStatic;
                    var setter = property.SetMethod is { IsPublic: true } set ?
                        set.ReturnParameter.GetRequiredCustomModifiers().Contains(typeof(IsExternalInit)) ? " init;" : " set;" : "";
                    signature = (isStatic ? "static " : "") + result + " " + key + " {" +
                        (property.GetMethod is { IsPublic: true } ? " get;" : "") + setter + " }";
                }
                else
                {
                    var field = (FieldInfo)member; result = TypeName(field.FieldType, nullability.Create(field));
                    kind = type.IsEnum ? "Enum value" : "Constant"; isStatic = field.IsStatic;
                    signature = type.IsEnum ? key + " = " + Literal(field.GetRawConstantValue(), Enum.GetUnderlyingType(type)) :
                        (field.IsLiteral ? "const " : "static readonly ") + result + " " + key + " = " + Literal(field.GetRawConstantValue(), field.FieldType);
                }
                contracts.Add(new(key, name, member is ConstructorInfo ? name : member.Name, kind, signature, result, isStatic,
                    Array.AsReadOnly(parameters)));
            }
        }
        return contracts.OrderBy(c => c.Member, StringComparer.Ordinal).ThenBy(c => c.Signature, StringComparer.Ordinal).ToArray();
    }
    private static string TypeName(Type type, NullabilityInfo? info = null)
    {
        if (type.IsByRef) return TypeName(type.GetElementType()!, info);
        if (Nullable.GetUnderlyingType(type) is { } underlying) return TypeName(underlying) + "?";
        string name;
        if (type.IsArray) name = TypeName(type.GetElementType()!, info?.ElementType) + "[]";
        else if (type.IsGenericType) name = type.Name[..type.Name.IndexOf('`')] + "<" + string.Join(", ", type.GetGenericArguments()
            .Select((t, i) => TypeName(t, info?.GenericTypeArguments.ElementAtOrDefault(i)))) + ">";
        else name = type == typeof(void) ? "void" : type == typeof(string) ? "string" : type == typeof(bool) ? "bool" :
            type == typeof(int) ? "int" : type == typeof(uint) ? "uint" : type == typeof(long) ? "long" : type == typeof(byte) ? "byte" : type.Name;
        return name + (!type.IsValueType && info?.ReadState == NullabilityState.Nullable ? "?" : "");
    }
    private static string Literal(object? value, Type type)
    {
        if (value is null) return type.IsValueType && Nullable.GetUnderlyingType(type) is null ? "default" : "null";
        if (value is DBNull or Missing) return "default";
        if (value is string text) return JsonSerializer.Serialize(text);
        if (value is bool boolean) return boolean ? "true" : "false";
        if (type.IsEnum) return type.Name + "." + Enum.GetName(type, value);
        return Convert.ToString(value, CultureInfo.InvariantCulture) ?? "default";
    }
}
