namespace StaffBff.Services;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
internal sealed class DocumentProblemContractAttribute(
    int statusCode,
    bool retryable,
    params string[] codes) : Attribute
{
    public int StatusCode { get; } = statusCode;

    public bool Retryable { get; } = retryable;

    public IReadOnlyList<string> Codes { get; } = codes;
}
