namespace ByteEngine.Core.Variables;

public sealed class VariableReference
{
    public VariableScope Scope { get; set; }
    public Guid? ObjectId { get; set; }
    public string? ObjectName { get; set; }
    public string? ComponentType { get; set; }
    public string MemberName { get; set; } = string.Empty;
}
