namespace Hoard.Architecture.Tests;

public sealed class CoreDependencyRulesFixtureTests
{
    [Theory]
    [InlineData("persistence.json", "Hoard.Persistence.Sqlite", "Hoard.Persistence.*")]
    [InlineData("storage.json", "Hoard.Storage.FileSystem", "Hoard.Storage.*")]
    [InlineData("protocols.json", "Hoard.Protocols.NuGet", "Hoard.Protocols.*")]
    [InlineData("identity.json", "Hoard.Identity.Entra", "Hoard.Identity.*")]
    [InlineData("efcore.json", "Microsoft.EntityFrameworkCore.Sqlite", "Microsoft.EntityFrameworkCore*")]
    public void Forbidden_reference_fails_and_names_the_rule(string fixture, string name, string rule)
    {
        var violations = CoreDependencyRules.Evaluate(Fixture(fixture));

        Assert.Equal([$"Hoard.Core must not reference '{name}' (rule: {rule})"], violations);
    }

    [Fact]
    public void Transitive_reference_fails()
    {
        var violations = CoreDependencyRules.Evaluate(Fixture("transitive.json"));

        Assert.Equal(
            ["Hoard.Core must not reference 'Microsoft.EntityFrameworkCore' (rule: Microsoft.EntityFrameworkCore*)"],
            violations);
    }

    [Fact]
    public void Renamed_package_id_cannot_hide_a_forbidden_project()
    {
        var violations = CoreDependencyRules.Evaluate(Fixture("renamed-project.json"));

        Assert.Equal(
            ["Hoard.Core must not reference 'Hoard.Persistence.Sqlite' (rule: Hoard.Persistence.*)"],
            violations);
    }

    [Fact]
    public void Hoard_project_other_than_abstractions_fails()
    {
        var violations = CoreDependencyRules.Evaluate(Fixture("hosting.json"));

        Assert.Equal(
            ["Hoard.Core must not reference 'Hoard.Hosting' (rule: Hoard.* except Hoard.Abstractions)"],
            violations);
    }

    [Fact]
    public void Hoard_abstractions_is_allowed()
    {
        Assert.Empty(CoreDependencyRules.Evaluate(Fixture("abstractions.json")));
    }

    [Fact]
    public void Matching_is_case_insensitive()
    {
        var violations = CoreDependencyRules.Evaluate(Fixture("case-insensitive.json"));

        Assert.Equal(
            ["Hoard.Core must not reference 'hoard.persistence.sqlite' (rule: Hoard.Persistence.*)"],
            violations);
    }

    [Fact]
    public void Assets_file_for_another_project_fails()
    {
        var violations = CoreDependencyRules.Evaluate(Fixture("wrong-project.json"));

        var violation = Assert.Single(violations);
        Assert.Contains("belongs to 'Hoard.Host', not Hoard.Core", violation);
    }

    [Fact]
    public void Missing_assets_file_fails()
    {
        var violations = CoreDependencyRules.Evaluate(Fixture("does-not-exist.json"));

        var violation = Assert.Single(violations);
        Assert.Contains("not found", violation);
    }

    private static string Fixture(string name) => Path.Combine(AppContext.BaseDirectory, "Fixtures", name);
}
