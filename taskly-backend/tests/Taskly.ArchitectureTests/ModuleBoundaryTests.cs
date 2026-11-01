using System.Reflection;
using NetArchTest.Rules;
using Taskly.Modules.Tasks;
using Taskly.Modules.Users;
using Taskly.SharedKernel;

namespace Taskly.ArchitectureTests;

public sealed class ModuleBoundaryTests
{
    private static readonly string[] Frameworks =
    [
        "Microsoft.AspNetCore",
        "Npgsql",
        "BCrypt",
        "Microsoft.IdentityModel"
    ];

    public static TheoryData<string, Assembly> Modules => new()
    {
        { "Users", typeof(UsersModule).Assembly },
        { "Tasks", typeof(TasksModule).Assembly }
    };

    [Theory]
    [MemberData(nameof(Modules))]
    public void DomainDependsOnNeitherOuterLayersNorFrameworks(string module, Assembly assembly)
    {
        var forbidden = Layers(module, "Application", "Infrastructure", "Presentation")
            .Concat(Frameworks)
            .Append("Taskly.SharedKernel.Infrastructure")
            .ToArray();

        AssertNoDependencies(assembly, $"Taskly.Modules.{module}.Domain", forbidden);
    }

    [Theory]
    [MemberData(nameof(Modules))]
    public void ApplicationDependsOnNeitherInfrastructureNorPresentation(string module, Assembly assembly)
    {
        var forbidden = Layers(module, "Infrastructure", "Presentation")
            .Concat(Frameworks)
            .Append("Taskly.SharedKernel.Infrastructure")
            .ToArray();

        AssertNoDependencies(assembly, $"Taskly.Modules.{module}.Application", forbidden);
    }

    [Theory]
    [MemberData(nameof(Modules))]
    public void InfrastructureDoesNotDependOnPresentation(string module, Assembly assembly)
    {
        AssertNoDependencies(assembly, $"Taskly.Modules.{module}.Infrastructure", Layers(module, "Presentation"));
    }

    [Fact]
    public void UsersAndTasksDoNotReferenceEachOther()
    {
        AssertAssemblyHasNoDependencies(typeof(UsersModule).Assembly, ["Taskly.Modules.Tasks"]);
        AssertAssemblyHasNoDependencies(typeof(TasksModule).Assembly, ["Taskly.Modules.Users"]);

        var usersReferences = typeof(UsersModule).Assembly.GetReferencedAssemblies().Select(name => name.Name);
        var tasksReferences = typeof(TasksModule).Assembly.GetReferencedAssemblies().Select(name => name.Name);

        Assert.DoesNotContain("Taskly.Modules.Tasks", usersReferences);
        Assert.DoesNotContain("Taskly.Modules.Users", tasksReferences);
    }

    [Fact]
    public void ModulesDoNotReferenceTheHost()
    {
        foreach (var assembly in new[] { typeof(UsersModule).Assembly, typeof(TasksModule).Assembly, typeof(SharedKernelServiceCollectionExtensions).Assembly })
        {
            Assert.DoesNotContain("Taskly.Host", assembly.GetReferencedAssemblies().Select(name => name.Name));
        }
    }

    [Fact]
    public void SharedKernelDoesNotDependOnAnyModule()
    {
        AssertAssemblyHasNoDependencies(
            typeof(SharedKernelServiceCollectionExtensions).Assembly,
            ["Taskly.Modules", "Taskly.Host"]);
    }

    [Fact]
    public void SharedKernelDomainStaysFreeOfInfrastructure()
    {
        AssertNoDependencies(
            typeof(SharedKernelServiceCollectionExtensions).Assembly,
            "Taskly.SharedKernel.Domain",
            ["Taskly.SharedKernel.Infrastructure", "Taskly.SharedKernel.Application", "Microsoft.AspNetCore", "Npgsql"]);
    }

    private static string[] Layers(string module, params string[] layers) =>
        layers.Select(layer => $"Taskly.Modules.{module}.{layer}").ToArray();

    private static void AssertAssemblyHasNoDependencies(Assembly assembly, string[] forbidden)
    {
        var result = Types.InAssembly(assembly)
            .ShouldNot()
            .HaveDependencyOnAny(forbidden)
            .GetResult();

        Assert.True(
            result.IsSuccessful,
            $"{assembly.GetName().Name} has forbidden dependencies: {string.Join(", ", result.FailingTypeNames ?? [])}");
    }

    private static void AssertNoDependencies(Assembly assembly, string @namespace, string[] forbidden)
    {
        var result = Types.InAssembly(assembly)
            .That()
            .ResideInNamespace(@namespace)
            .ShouldNot()
            .HaveDependencyOnAny(forbidden)
            .GetResult();

        Assert.True(
            result.IsSuccessful,
            $"{@namespace} has forbidden dependencies: {string.Join(", ", result.FailingTypeNames ?? [])}");
    }
}
