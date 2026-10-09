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
                .ShouldNot()
                .BePublic()
                .GetResult();

            Assert.True(result.IsSuccessful, $"Assembly {assembly.GetName().Name} contains public types other than Module class.");
        }
    }

    [Fact]
    public void Modules_ShouldNotDependOnOtherModulesNamespaces()
    {
        var moduleNamespaces = new[]
        {
            "ShopFlow.Modules.Identity",
            "ShopFlow.Modules.Catalog",
            "ShopFlow.Modules.Inventory",
            "ShopFlow.Modules.Ordering",
            "ShopFlow.Modules.Payment",
            "ShopFlow.Modules.Notification"
        };

        foreach (var assembly in ModuleAssemblies)
        {
            var currentModuleNamespace = assembly.GetName().Name!;
            var otherModulesNamespaces = moduleNamespaces.Where(x => x != currentModuleNamespace).ToArray();

            var result = Types.InAssembly(assembly)
                .ShouldNot()
                .HaveDependencyOnAny(otherModulesNamespaces)
                .GetResult();

            Assert.True(result.IsSuccessful, $"Assembly {currentModuleNamespace} has dependencies on other modules' namespaces.");
        }
    }
}
