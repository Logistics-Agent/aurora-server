namespace RegulatoryCompliance.Tests;

public sealed class ServiceFoundationTests
{
    [Fact]
    public void ServiceAndTestAssembliesLoad()
    {
        var serviceAssembly = typeof(Program).Assembly;
        var testAssembly = typeof(ServiceFoundationTests).Assembly;

        Assert.Equal("RegulatoryCompliance", serviceAssembly.GetName().Name);
        Assert.Equal("RegulatoryCompliance.Tests", testAssembly.GetName().Name);
    }
}
