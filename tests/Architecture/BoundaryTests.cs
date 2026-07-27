// BoundaryTests — rules the compiler cannot express, about what Core is allowed
// to depend on (ADR-014).
//
// Use:  runs with the normal test suite; a violation fails the build.
// Edit: add a rule whenever a boundary breach reaches code review — a breach a
//       human had to catch is a missing test. Rehearse new rules by breaking
//       them deliberately; see the README in this folder.

using FluentAssertions;
using NetArchTest.Rules;
using OpenDealer360.Core;
using Xunit;

namespace OpenDealer360.ArchitectureTests;

/// <summary>
/// Executable boundary rules (ADR-014). These fail the build when a dependency
/// crosses a forbidden line. More rules are added as modules land — a business
/// module referencing another module's internals, Integrations referencing a
/// module, Domain referencing ASP.NET/EF, and so on (doc 03 §5).
/// </summary>
public sealed class BoundaryTests
{
    private static readonly System.Reflection.Assembly Core = typeof(Result).Assembly;

    [Fact]
    public void Core_must_not_depend_on_web_or_persistence_frameworks()
    {
        var result = Types.InAssembly(Core)
            .Should()
            .NotHaveDependencyOnAny(
                "Microsoft.AspNetCore",
                "Microsoft.EntityFrameworkCore",
                "OpenDealer360.Host")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            because: "Core is a domain-free kernel; offenders: "
                + string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Fact]
    public void Core_must_not_reference_module_namespaces()
    {
        // No business concept (Deal, RepairOrder, Rooftop entities, Journal, ...)
        // may live in or be referenced by Core (doc 03 §4).
        var result = Types.InAssembly(Core)
            .Should()
            .NotHaveDependencyOn("OpenDealer360.Modules")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            because: "Core must not know about business modules; offenders: "
                + string.Join(", ", result.FailingTypeNames ?? []));
    }
}
