namespace GpsTracking.Tests;

public sealed class ServiceFoundationTests
{
    [Fact]
    public void ServiceAssemblyLoads()
    {
        var assembly = typeof(Program).Assembly;

        Assert.Equal("GpsTracking", assembly.GetName().Name);
    }
}
