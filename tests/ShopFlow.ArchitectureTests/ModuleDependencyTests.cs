using System.Reflection;
using NetArchTest.Rules;
using ShopFlow.Modules.Catalog;
using ShopFlow.Modules.Identity;
using ShopFlow.Modules.Inventory;
using ShopFlow.Modules.Ordering;
using ShopFlow.Modules.Payment;
using ShopFlow.Modules.Notification;
using Xunit;

namespace ShopFlow.ArchitectureTests;

public class ModuleDependencyTests
{
    private static readonly Assembly[] ModuleAssemblies =
    {
        typeof(IdentityModule).Assembly,
        typeof(CatalogModule).Assembly,
        typeof(InventoryModule).Assembly,
        typeof(OrderingModule).Assembly,
        typeof(PaymentModule).Assembly,
        typeof(NotificationModule).Assembly
    };

    private static readonly Assembly[] ContractsAssemblies =
    {
        Assembly.Load("ShopFlow.Modules.Identity.Contracts"),
        Assembly.Load("ShopFlow.Modules.Catalog.Contracts"),
        Assembly.Load("ShopFlow.Modules.Inventory.Contracts"),
        Assembly.Load("ShopFlow.Modules.Ordering.Contracts"),
        Assembly.Load("ShopFlow.Modules.Payment.Contracts"),
        Assembly.Load("ShopFlow.Modules.Notification.Contracts")
    };

    [Fact]
    public void Modules_ShouldOnlyReferenceAllowedAssemblies()
    {
        var allowedDependencies = new Dictionary<string, string[]>
        {
            ["ShopFlow.Modules.Identity"] = new[] { "ShopFlow.BuildingBlocks", "ShopFlow.Modules.Identity.Contracts" },
            ["ShopFlow.Modules.Catalog"] = new[] { "ShopFlow.BuildingBlocks", "ShopFlow.Modules.Catalog.Contracts" },
            ["ShopFlow.Modules.Inventory"] = new[] { "ShopFlow.BuildingBlocks", "ShopFlow.Modules.Inventory.Contracts" },
            ["ShopFlow.Modules.Payment"] = new[] { "ShopFlow.BuildingBlocks", "ShopFlow.Modules.Payment.Contracts" },
            ["ShopFlow.Modules.Ordering"] = new[] { "ShopFlow.BuildingBlocks", "ShopFlow.Modules.Ordering.Contracts", "ShopFlow.Modules.Catalog.Contracts", "ShopFlow.Modules.Inventory.Contracts", "ShopFlow.Modules.Payment.Contracts", "ShopFlow.Modules.Notification.Contracts" },
            ["ShopFlow.Modules.Notification"] = new[] { "ShopFlow.BuildingBlocks", "ShopFlow.Modules.Notification.Contracts", "ShopFlow.Modules.Ordering.Contracts" }
        };

        foreach (var assembly in ModuleAssemblies)
        {
            var assemblyName = assembly.GetName().Name!;
            var referencedAssemblies = assembly.GetReferencedAssemblies()
                .Select(x => x.Name!)
                .Where(x => x.StartsWith("ShopFlow."))
                .ToList();

            var allowedForModule = allowedDependencies[assemblyName];

            foreach (var referenced in referencedAssemblies)
            {
                Assert.True(allowedForModule.Contains(referenced), 
                    $"Assembly {assemblyName} is not allowed to reference {referenced}");
            }
        }
    }

    [Fact]
    public void Contracts_ShouldNotReferenceOtherShopFlowAssemblies()
    {
        foreach (var assembly in ContractsAssemblies)
        {
            var referencedAssemblies = assembly.GetReferencedAssemblies()
                .Select(x => x.Name!)
                .Where(x => x.StartsWith("ShopFlow."))
                .ToList();

            Assert.Empty(referencedAssemblies);
        }
    }

    [Fact]
    public void ModuleTypes_ShouldNotBePublic_ExceptModuleClass()
    {
        foreach (var assembly in ModuleAssemblies)
        {
            var result = Types.InAssembly(assembly)
                .That()
                .DoNotHaveNameEndingWith("Module")
                .And()
                .DoNotInherit(typeof(Microsoft.EntityFrameworkCore.Migrations.Migration))
                .And()
                .DoNotInherit(typeof(Microsoft.EntityFrameworkCore.Infrastructure.ModelSnapshot))
                .And()
                .DoNotResideInNamespace("ShopFlow.Modules.Inventory.Protos")
                .And()
                .DoNotHaveNameMatching(".*InventoryService.*")
                .ShouldNot()
                .BePublic()
                .GetResult();

            var failingTypes = result.FailingTypeNames != null ? string.Join(", ", result.FailingTypeNames) : "";
            Assert.True(result.IsSuccessful, $"Assembly {assembly.GetName().Name} contains public types other than Module class: {failingTypes}");
        }
    }

    [Fact]
    public void Modules_ShouldNotDependOnOtherModulesNamespaces()
    {
        var implementationNamespaces = new[]
        {
            "ShopFlow.Modules.Identity.Domain",
            "ShopFlow.Modules.Identity.Application",
            "ShopFlow.Modules.Identity.Infrastructure",
            "ShopFlow.Modules.Catalog.Domain",
            "ShopFlow.Modules.Catalog.Application",
            "ShopFlow.Modules.Catalog.Infrastructure",
            "ShopFlow.Modules.Inventory.Domain",
            "ShopFlow.Modules.Inventory.Application",
            "ShopFlow.Modules.Inventory.Infrastructure",
            "ShopFlow.Modules.Ordering.Domain",
            "ShopFlow.Modules.Ordering.Application",
            "ShopFlow.Modules.Ordering.Infrastructure",
            "ShopFlow.Modules.Payment.Domain",
            "ShopFlow.Modules.Payment.Application",
            "ShopFlow.Modules.Payment.Infrastructure",
            "ShopFlow.Modules.Notification.Domain",
            "ShopFlow.Modules.Notification.Application",
            "ShopFlow.Modules.Notification.Infrastructure",
            "ShopFlow.Modules.Notification.Consumers"
        };

        foreach (var assembly in ModuleAssemblies)
        {
            var currentModulePrefix = assembly.GetName().Name! + ".";
            var otherModulesNamespaces = implementationNamespaces
                .Where(x => !x.StartsWith(currentModulePrefix))
                .ToArray();

            var result = Types.InAssembly(assembly)
                .ShouldNot()
                .HaveDependencyOnAny(otherModulesNamespaces)
                .GetResult();

            Assert.True(result.IsSuccessful, $"Assembly {assembly.GetName().Name} has forbidden dependencies on other modules' internal namespaces.");
        }
    }
}
