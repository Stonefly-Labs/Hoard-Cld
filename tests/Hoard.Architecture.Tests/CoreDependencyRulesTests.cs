using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Xml.Linq;

namespace Hoard.Architecture.Tests;

public sealed class CoreDependencyRulesTests
{
    [Fact]
    public void Core_restore_graph_breaks_no_rule()
    {
        Assert.Empty(CoreDependencyRules.Evaluate(CoreMetadata("CoreAssetsFile")));
    }

    [Fact]
    public void Core_project_has_no_raw_assembly_references()
    {
        var project = XDocument.Load(CoreMetadata("CoreProjectFile"));

        Assert.DoesNotContain(project.Descendants(), e => e.Name.LocalName == "Reference");
    }

    [Fact]
    public void Core_assembly_references_break_no_rule()
    {
        using var stream = File.OpenRead(CoreMetadata("CoreAssemblyFile"));
        using var pe = new PEReader(stream);
        var metadata = pe.GetMetadataReader();
        var names = metadata.AssemblyReferences
            .Select(handle => metadata.GetString(metadata.GetAssemblyReference(handle).Name));

        Assert.Empty(CoreDependencyRules.EvaluateNames(names));
    }

    private static string CoreMetadata(string key) =>
        typeof(CoreDependencyRulesTests).Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .Single(a => a.Key == key)
            .Value!;
}
