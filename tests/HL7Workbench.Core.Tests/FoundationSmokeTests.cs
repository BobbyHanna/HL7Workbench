using System.Reflection;
using Xunit;

namespace HL7Workbench.Core.Tests;

public sealed class FoundationSmokeTests
{
    [Fact]
    public void CoreAssemblyCanBeLoadedWithoutUiDependencies()
    {
        var assembly = Assembly.Load("HL7Workbench.Core");

        Assert.Equal("HL7Workbench.Core", assembly.GetName().Name);
        Assert.DoesNotContain(assembly.GetReferencedAssemblies(),
            reference => reference.Name?.StartsWith("Avalonia", StringComparison.Ordinal) == true);
    }
}
