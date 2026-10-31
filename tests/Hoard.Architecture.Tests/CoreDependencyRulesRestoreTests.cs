using System.Diagnostics;

namespace Hoard.Architecture.Tests;

public sealed class CoreDependencyRulesRestoreTests : IDisposable
{
    // Outside the repository, so its Directory.*.props and global.json are not imported.
    private readonly string _root = Directory.CreateTempSubdirectory("hoard-arch-").FullName;

    [Fact]
    public async Task Real_restore_of_a_forbidden_project_reference_fails()
    {
        WriteProject("Hoard.Persistence.Fake", "");
        WriteProject(
            "Hoard.Core",
            """<ItemGroup><ProjectReference Include="../Hoard.Persistence.Fake/Hoard.Persistence.Fake.csproj" /></ItemGroup>""");

        // A relative path from the temp root keeps both projects on the same real path (macOS
        // links /var to /private/var, and NuGet drops a reference it resolves differently).
        var restore = Process.Start(new ProcessStartInfo("dotnet", ["restore", Path.Combine("Hoard.Core", "Hoard.Core.csproj")])
        {
            WorkingDirectory = _root,
            RedirectStandardOutput = true,
        })!;
        var output = await restore.StandardOutput.ReadToEndAsync(TestContext.Current.CancellationToken);
        await restore.WaitForExitAsync(TestContext.Current.CancellationToken);
        Assert.True(restore.ExitCode == 0, $"dotnet restore failed:{Environment.NewLine}{output}");

        var violations = CoreDependencyRules.Evaluate(Path.Combine(_root, "Hoard.Core", "obj", "project.assets.json"));

        Assert.Equal(
            ["Hoard.Core must not reference 'Hoard.Persistence.Fake' (rule: Hoard.Persistence.*)"],
            violations);
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private void WriteProject(string name, string items)
    {
        var dir = Directory.CreateDirectory(Path.Combine(_root, name)).FullName;
        File.WriteAllText(
            Path.Combine(dir, $"{name}.csproj"),
            $"""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup>
              {items}
            </Project>
            """);
    }
}
