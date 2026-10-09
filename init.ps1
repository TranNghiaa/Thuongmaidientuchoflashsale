$dirs = @(
    "src/ShopFlow.Host",
    "src/ShopFlow.BuildingBlocks",
    "src/Modules/Identity",
    "src/Modules/Catalog",
    "src/Modules/Inventory",
    "src/Modules/Ordering",
    "src/Modules/Payment",
    "src/Modules/Notification",
    "docs/spec",
    "docs/evidence",
    "tools",
    "tests/ShopFlow.ArchitectureTests",
    "tests/ShopFlow.UnitTests",
    "tests/ShopFlow.IntegrationTests"
)

foreach ($dir in $dirs) {
    New-Item -ItemType Directory -Force -Path $dir | Out-Null
}

Move-Item -Path "*.md" -Destination "docs/spec/" -Force -ErrorAction SilentlyContinue

dotnet new sln -n ShopFlow

# Create Host and BuildingBlocks
dotnet new web -n ShopFlow.Host -o src/ShopFlow.Host
dotnet new classlib -n ShopFlow.BuildingBlocks -o src/ShopFlow.BuildingBlocks

# Create modules
$modules = @("Identity", "Catalog", "Inventory", "Ordering", "Payment", "Notification")
foreach ($mod in $modules) {
    dotnet new classlib -n "ShopFlow.Modules.${mod}.Contracts" -o "src/Modules/${mod}/ShopFlow.Modules.${mod}.Contracts"
    dotnet new classlib -n "ShopFlow.Modules.${mod}" -o "src/Modules/${mod}/ShopFlow.Modules.${mod}"
}

# Create test projects
dotnet new xunit -n ShopFlow.ArchitectureTests -o tests/ShopFlow.ArchitectureTests
dotnet new xunit -n ShopFlow.UnitTests -o tests/ShopFlow.UnitTests
dotnet new xunit -n ShopFlow.IntegrationTests -o tests/ShopFlow.IntegrationTests

# Add everything to sln
Get-ChildItem -Recurse -Filter "*.csproj" | ForEach-Object {
    dotnet sln ShopFlow.sln add $_.FullName
}

# Add dependencies
# 1. BuildingBlocks to all Modules and Host
foreach ($mod in $modules) {
    dotnet add src/Modules/${mod}/ShopFlow.Modules.${mod} reference src/ShopFlow.BuildingBlocks/ShopFlow.BuildingBlocks.csproj
}
dotnet add src/ShopFlow.Host reference src/ShopFlow.BuildingBlocks/ShopFlow.BuildingBlocks.csproj

# 2. Modules to their own Contracts
foreach ($mod in $modules) {
    dotnet add src/Modules/${mod}/ShopFlow.Modules.${mod} reference src/Modules/${mod}/ShopFlow.Modules.${mod}.Contracts/ShopFlow.Modules.${mod}.Contracts.csproj
}

# 3. Host to all Modules
foreach ($mod in $modules) {
    dotnet add src/ShopFlow.Host reference src/Modules/${mod}/ShopFlow.Modules.${mod}/ShopFlow.Modules.${mod}.csproj
}

# 4. Ordering references Catalog, Inventory, Payment Contracts
dotnet add src/Modules/Ordering/ShopFlow.Modules.Ordering reference src/Modules/Catalog/ShopFlow.Modules.Catalog.Contracts/ShopFlow.Modules.Catalog.Contracts.csproj
dotnet add src/Modules/Ordering/ShopFlow.Modules.Ordering reference src/Modules/Inventory/ShopFlow.Modules.Inventory.Contracts/ShopFlow.Modules.Inventory.Contracts.csproj
dotnet add src/Modules/Ordering/ShopFlow.Modules.Ordering reference src/Modules/Payment/ShopFlow.Modules.Payment.Contracts/ShopFlow.Modules.Payment.Contracts.csproj

# 5. Notification references Ordering.Contracts
dotnet add src/Modules/Notification/ShopFlow.Modules.Notification reference src/Modules/Ordering/ShopFlow.Modules.Ordering.Contracts/ShopFlow.Modules.Ordering.Contracts.csproj

# 6. Tests reference everything (Wait, architecture test should reference everything?)
foreach ($mod in $modules) {
    dotnet add tests/ShopFlow.ArchitectureTests reference src/Modules/${mod}/ShopFlow.Modules.${mod}/ShopFlow.Modules.${mod}.csproj
    dotnet add tests/ShopFlow.UnitTests reference src/Modules/${mod}/ShopFlow.Modules.${mod}/ShopFlow.Modules.${mod}.csproj
    dotnet add tests/ShopFlow.IntegrationTests reference src/Modules/${mod}/ShopFlow.Modules.${mod}/ShopFlow.Modules.${mod}.csproj
}
dotnet add tests/ShopFlow.ArchitectureTests reference src/ShopFlow.Host/ShopFlow.Host.csproj
dotnet add tests/ShopFlow.ArchitectureTests reference src/ShopFlow.BuildingBlocks/ShopFlow.BuildingBlocks.csproj

# Delete default files
Get-ChildItem -Recurse -Filter "Class1.cs" | Remove-Item -Force
