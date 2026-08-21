namespace DocumentOcr.Tests;

public sealed class ServiceFoundationTests
{
    [Fact]
    public void ServiceAndTestAssembliesLoad()
    {
        var serviceAssembly = typeof(Program).Assembly;
        var testAssembly = typeof(ServiceFoundationTests).Assembly;

        Assert.Equal("DocumentOcr", serviceAssembly.GetName().Name);
        Assert.Equal("DocumentOcr.Tests", testAssembly.GetName().Name);
    }
}
