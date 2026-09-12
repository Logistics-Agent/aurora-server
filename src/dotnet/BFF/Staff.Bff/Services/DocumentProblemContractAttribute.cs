namespace StaffBff.Services;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
internal sealed class DocumentProblemContractAttribute(
    params string[] operationIds) : Attribute
{
    public IReadOnlyList<string> OperationIds { get; } = operationIds;
}
