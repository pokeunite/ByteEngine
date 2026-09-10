using System.Numerics;
using System.Reflection;
using ByteEngine.Core.Scene;

namespace ByteEngine.Core.Variables;

public sealed class VariableResolutionContext
{
    public required VariableStore Globals { get; init; }
    public required Scene.Scene Scene { get; init; }
    public GameObject? Self { get; init; }
}

public static class VariableResolver
{
    public static bool TryGet(VariableReference reference,VariableResolutionContext context,out object? value)
    {
        value=null; ArgumentNullException.ThrowIfNull(reference); ArgumentNullException.ThrowIfNull(context);
        VariableStore? store=reference.Scope switch { VariableScope.Global=>context.Globals,VariableScope.Scene=>context.Scene.Variables,VariableScope.Self=>context.Self?.Variables,VariableScope.Object=>ResolveObject(reference,context)?.Variables,_=>null };
        if(store!=null&&store.TryGet(reference.MemberName,out VariableValue? variable)){value=variable!.BoxedValue;return true;}
        if(reference.Scope!=VariableScope.Component)return false;
        GameObject? target=ResolveObject(reference,context)??context.Self;
        object? root=reference.ComponentType is "Transform" ? target?.Transform : target?.Components.FirstOrDefault(c=>MatchesType(c.GetType(),reference.ComponentType));
        return root!=null&&TryReadPath(root,reference.MemberName,out value);
    }

    public static bool TrySet(VariableReference reference,VariableResolutionContext context,object? value)
    {
        VariableStore? store=reference.Scope switch { VariableScope.Global=>context.Globals,VariableScope.Scene=>context.Scene.Variables,VariableScope.Self=>context.Self?.Variables,VariableScope.Object=>ResolveObject(reference,context)?.Variables,_=>null };
        if(store!=null&&store.TryGet(reference.MemberName,out VariableValue? variable))return AssignVariable(variable!,value);
        if(reference.Scope!=VariableScope.Component)return false;
        GameObject? target=ResolveObject(reference,context)??context.Self;
        object? root=reference.ComponentType is "Transform"?target?.Transform:target?.Components.FirstOrDefault(c=>MatchesType(c.GetType(),reference.ComponentType));
        return root!=null&&TryWritePath(root,reference.MemberName,value);
    }

    private static GameObject? ResolveObject(VariableReference reference,VariableResolutionContext context)=>reference.ObjectId.HasValue?context.Scene.FindGameObject(reference.ObjectId.Value):!string.IsNullOrWhiteSpace(reference.ObjectName)?context.Scene.FindGameObject(reference.ObjectName):null;
    private static bool MatchesType(Type type,string? name)=>!string.IsNullOrWhiteSpace(name)&&(type.Name.Equals(name,StringComparison.OrdinalIgnoreCase)||type.FullName?.Equals(name,StringComparison.OrdinalIgnoreCase)==true);
    private static bool TryReadPath(object root,string path,out object? value){object? current=root;foreach(string segment in path.Split('.',StringSplitOptions.RemoveEmptyEntries)){if(current==null){value=null;return false;}PropertyInfo? p=current.GetType().GetProperty(segment,BindingFlags.Instance|BindingFlags.Public|BindingFlags.IgnoreCase);FieldInfo? f=current.GetType().GetField(segment,BindingFlags.Instance|BindingFlags.Public|BindingFlags.IgnoreCase);if(p==null&&f==null){value=null;return false;}current=p?.GetValue(current)??f?.GetValue(current);}value=current;return true;}
    private static bool TryWritePath(object root,string path,object? value){string[] segments=path.Split('.',StringSplitOptions.RemoveEmptyEntries);if(segments.Length==0)return false;object current=root;for(int i=0;i<segments.Length-1;i++){if(!TryReadPath(current,segments[i],out object? next)||next==null)return false;current=next;}PropertyInfo? p=current.GetType().GetProperty(segments[^1],BindingFlags.Instance|BindingFlags.Public|BindingFlags.IgnoreCase);if(p?.CanWrite==true){p.SetValue(current,ConvertValue(value,p.PropertyType));return true;}FieldInfo? f=current.GetType().GetField(segments[^1],BindingFlags.Instance|BindingFlags.Public|BindingFlags.IgnoreCase);if(f!=null){f.SetValue(current,ConvertValue(value,f.FieldType));return true;}return false;}
    private static object? ConvertValue(object? value,Type target)=>value==null?null:target.IsInstanceOfType(value)?value:Convert.ChangeType(value,target);
    private static bool AssignVariable(VariableValue variable,object? value){try{switch(variable.Type){case VariableType.Number:variable.Number=Convert.ToDouble(value);break;case VariableType.String:variable.String=Convert.ToString(value)??string.Empty;break;case VariableType.Boolean:variable.Boolean=Convert.ToBoolean(value);break;case VariableType.Vector2:if(value is not Vector2 v2)return false;variable.Vector2=v2;break;case VariableType.Vector3:if(value is not Vector3 v3)return false;variable.Vector3=v3;break;}return true;}catch{return false;}}
}
