using System.Text.Json;

namespace Hoard.Architecture.Tests;

/// <summary>
/// Hoard.Core's dependency rules (docs/ARCHITECTURE.md §3), evaluated against its restore
/// graph so that direct, transitive and not-yet-existing references are all caught.
/// </summary>
public static class CoreDependencyRules
{
    private static readonly (string Prefix, string Rule)[] DenyPrefixes =
    [
        ("Hoard.Persistence.", "Hoard.Persistence.*"),
        ("Hoard.Storage.", "Hoard.Storage.*"),
        ("Hoard.Protocols.", "Hoard.Protocols.*"),
        ("Hoard.Identity.", "Hoard.Identity.*"),
        ("Microsoft.EntityFrameworkCore", "Microsoft.EntityFrameworkCore*"),
    ];

    private const string AllowedHoardReference = "Hoard.Abstractions";

    /// <summary>Evaluates a <c>project.assets.json</c> written by restoring Hoard.Core.</summary>
    public static IReadOnlyList<string> Evaluate(string assetsJsonPath)
    {
        if (!File.Exists(assetsJsonPath))
        {
            return [$"Hoard.Core project assets file '{assetsJsonPath}' not found; restore Hoard.Core first"];
        }

        using var assets = JsonDocument.Parse(File.ReadAllText(assetsJsonPath));
        var root = assets.RootElement;

        var projectName = root.GetProperty("project").GetProperty("restore").GetProperty("projectName").GetString();
        if (projectName != "Hoard.Core")
        {
            return [$"project assets file '{assetsJsonPath}' belongs to '{projectName}', not Hoard.Core"];
        }

        var names = new List<string>();
        foreach (var library in root.GetProperty("libraries").EnumerateObject())
        {
            names.Add(library.Name.Split('/')[0]);

            // A project reference can rename its PackageId; its project file name cannot hide.
            if (library.Value.GetProperty("type").GetString() == "project"
                && library.Value.TryGetProperty("path", out var path))
            {
                names.Add(Path.GetFileNameWithoutExtension(path.GetString()!));
            }
        }

        return EvaluateNames(names);
    }

    /// <summary>Evaluates assembly, package or project names Hoard.Core references.</summary>
    public static IReadOnlyList<string> EvaluateNames(IEnumerable<string> names) =>
        names
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(name => (Name: name, Rule: BrokenRule(name)))
            .Where(r => r.Rule is not null)
            .Select(r => $"Hoard.Core must not reference '{r.Name}' (rule: {r.Rule})")
            .ToList();

    private static string? BrokenRule(string name)
    {
        foreach (var (prefix, rule) in DenyPrefixes)
        {
            if (name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return rule;
            }
        }

        var isHoard = name.StartsWith("Hoard.", StringComparison.OrdinalIgnoreCase);
        var isAllowed = name.Equals(AllowedHoardReference, StringComparison.OrdinalIgnoreCase);
        return isHoard && !isAllowed ? $"Hoard.* except {AllowedHoardReference}" : null;
    }
}
