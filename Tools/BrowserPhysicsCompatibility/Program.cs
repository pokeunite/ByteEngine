using Mono.Cecil;
using Mono.Cecil.Cil;
using System.Numerics;
using ByteEngine.WebCompatibility;
if(args.Length==1&&args[0]=="--self-test"){
    var random=new Random(42);float[] special=[float.NaN,float.PositiveInfinity,float.NegativeInfinity,0f,-0f,1f,-1f,float.Epsilon];
    var methods=typeof(VectorMasks).GetMethods().Where(m=>m.DeclaringType==typeof(VectorMasks)).ToArray();
    for(int trial=0;trial<1000;trial++){
        float[] a=new float[Vector<float>.Count],b=new float[a.Length];for(int i=0;i<a.Length;i++){a[i]=trial<special.Length?special[(trial+i)%special.Length]:(float)(random.NextDouble()*20-10);b[i]=trial<special.Length?special[(trial+i+1)%special.Length]:(float)(random.NextDouble()*20-10);}
        var x=new Vector<float>(a);var y=new Vector<float>(b);
        foreach(var method in methods){var got=(Vector<int>)method.Invoke(null,[x,y])!;var want=method.Name switch{"GreaterThan"=>Vector.GreaterThan(x,y),"LessThan"=>Vector.LessThan(x,y),"GreaterThanOrEqual"=>Vector.GreaterThanOrEqual(x,y),"LessThanOrEqual"=>Vector.LessThanOrEqual(x,y),_=>Vector.Equals(x,y)};if(got!=want)throw new Exception("Mask mismatch: "+method.Name);}
    }
    Console.WriteLine($"PASS 5,000 mask comparisons including NaN, infinity and signed zero; lanes={Vector<float>.Count}");return;
}
if(args.Length!=1)throw new ArgumentException("Expected a trimmed browser physics assembly");
string path=Path.GetFullPath(args[0]);
if(!path.Replace('\\','/').Contains("/obj/")||Path.GetFileName(path) is not ("BepuPhysics.dll" or "BepuUtilities.dll"))throw new ArgumentException("Patch only generated browser physics assemblies");
using var module=ModuleDefinition.ReadModule(path,new ReaderParameters{InMemory=true});
const string helperName="ByteEngine.WebCompatibility.VectorMasks";
if(module.Types.Any(t=>t.FullName==helperName)){Console.WriteLine("Physics mask compatibility already applied");return;}
using var source=ModuleDefinition.ReadModule(typeof(VectorMasks).Assembly.Location);
var template=source.Types.Single(t=>t.FullName==helperName);
var helper=new TypeDefinition(template.Namespace,template.Name,template.Attributes,module.ImportReference(template.BaseType));
foreach(var original in template.Methods.Where(m=>m.HasBody)){
    var copy=new MethodDefinition(original.Name,original.Attributes,module.ImportReference(original.ReturnType)){ImplAttributes=original.ImplAttributes};
    foreach(var parameter in original.Parameters)copy.Parameters.Add(new ParameterDefinition(parameter.Name,parameter.Attributes,module.ImportReference(parameter.ParameterType)));
    copy.Body.InitLocals=original.Body.InitLocals;copy.Body.MaxStackSize=original.Body.MaxStackSize;
    foreach(var variable in original.Body.Variables)copy.Body.Variables.Add(new VariableDefinition(module.ImportReference(variable.VariableType)));
    var instructions=original.Body.Instructions.ToDictionary(i=>i,i=>Instruction.Create(OpCodes.Nop));
    foreach(var instruction in original.Body.Instructions){var clone=instructions[instruction];clone.OpCode=instruction.OpCode;clone.Operand=instruction.Operand switch{
        Instruction target=>instructions[target],Instruction[] targets=>targets.Select(t=>instructions[t]).ToArray(),
        VariableDefinition variable=>copy.Body.Variables[variable.Index],ParameterDefinition parameter=>copy.Parameters[parameter.Index],
        MethodReference method=>module.ImportReference(method),FieldReference field=>module.ImportReference(field),TypeReference type=>module.ImportReference(type),
        _=>instruction.Operand};copy.Body.Instructions.Add(clone);}
    if(original.Body.ExceptionHandlers.Count!=0)throw new InvalidOperationException("Unexpected helper exception handler");helper.Methods.Add(copy);
}
int changed=0;
IEnumerable<TypeDefinition> Types(IEnumerable<TypeDefinition> types){foreach(var t in types){yield return t;foreach(var child in Types(t.NestedTypes))yield return child;}}
foreach(var method in Types(module.Types).SelectMany(t=>t.Methods).Where(m=>m.HasBody && (m.DeclaringType.Namespace=="BepuPhysics.CollisionDetection.CollisionTasks" || module.Name=="BepuUtilities.dll")))foreach(var instruction in method.Body.Instructions){
    if(instruction.Operand is not MethodReference call||call.DeclaringType.FullName!="System.Numerics.Vector"||call.HasGenericParameters||call is GenericInstanceMethod||call.Parameters.Count!=2)continue;
    var replacement=helper.Methods.FirstOrDefault(m=>m.Name==call.Name);if(replacement==null)continue;
    if(call.Parameters[0].ParameterType.FullName!="System.Numerics.Vector`1<System.Single>"||call.ReturnType.FullName!="System.Numerics.Vector`1<System.Int32>")continue;
    instruction.Operand=replacement;changed++;
}
if(changed>0){module.Types.Add(helper);module.Write(path);}
Console.WriteLine($"Browser physics masks: {Path.GetFileName(path)}, {changed} calls adapted");
