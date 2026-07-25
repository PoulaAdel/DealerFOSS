using FluentAssertions;
using NetArchTest.Rules;
using OpenDealer360.Platform.Kernel;
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
    private static readonly System.Reflection.Assembly Platform = typeof(Result).Assembly;

    [Fact]
    public void Platform_must_not_depend_on_web_or_persistence_frameworks()
    {
        var result = Types.InAssembly(Platform)
            .Should()
            .NotHaveDependencyOnAny(
                "Microsoft.AspNetCore",
                "Microsoft.EntityFrameworkCore",
                "OpenDealer360.Host")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            because: "Platform is a domain-free kernel; offenders: "
                + string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Fact]
    public void Platform_kernel_must_not_reference_module_namespaces()
    {
        // No business concept (Deal, RepairOrder, Rooftop entities, Journal, ...)
        // may live in or be referenced by Platform (doc 03 §4).
        var result = Types.InAssembly(Platform)
            .Should()
            .NotHaveDependencyOn("OpenDealer360.Modules")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            because: "Platform must not know about business modules; offenders: "
                + string.Join(", ", result.FailingTypeNames ?? []));
    }
}
