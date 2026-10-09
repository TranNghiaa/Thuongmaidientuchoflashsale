FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["ShopFlow.sln", "./"]
COPY ["Directory.Build.props", "./"]
COPY ["Directory.Packages.props", "./"]

# Copy project files
COPY ["src/ShopFlow.Host/ShopFlow.Host.csproj", "src/ShopFlow.Host/"]
COPY ["src/ShopFlow.BuildingBlocks/ShopFlow.BuildingBlocks.csproj", "src/ShopFlow.BuildingBlocks/"]
COPY ["src/Modules/Identity/ShopFlow.Modules.Identity/ShopFlow.Modules.Identity.csproj", "src/Modules/Identity/ShopFlow.Modules.Identity/"]
COPY ["src/Modules/Identity/ShopFlow.Modules.Identity.Contracts/ShopFlow.Modules.Identity.Contracts.csproj", "src/Modules/Identity/ShopFlow.Modules.Identity.Contracts/"]
COPY ["src/Modules/Catalog/ShopFlow.Modules.Catalog/ShopFlow.Modules.Catalog.csproj", "src/Modules/Catalog/ShopFlow.Modules.Catalog/"]
COPY ["src/Modules/Catalog/ShopFlow.Modules.Catalog.Contracts/ShopFlow.Modules.Catalog.Contracts.csproj", "src/Modules/Catalog/ShopFlow.Modules.Catalog.Contracts/"]
COPY ["src/Modules/Inventory/ShopFlow.Modules.Inventory/ShopFlow.Modules.Inventory.csproj", "src/Modules/Inventory/ShopFlow.Modules.Inventory/"]
COPY ["src/Modules/Inventory/ShopFlow.Modules.Inventory.Contracts/ShopFlow.Modules.Inventory.Contracts.csproj", "src/Modules/Inventory/ShopFlow.Modules.Inventory.Contracts/"]
COPY ["src/Modules/Ordering/ShopFlow.Modules.Ordering/ShopFlow.Modules.Ordering.csproj", "src/Modules/Ordering/ShopFlow.Modules.Ordering/"]
COPY ["src/Modules/Ordering/ShopFlow.Modules.Ordering.Contracts/ShopFlow.Modules.Ordering.Contracts.csproj", "src/Modules/Ordering/ShopFlow.Modules.Ordering.Contracts/"]
COPY ["src/Modules/Payment/ShopFlow.Modules.Payment/ShopFlow.Modules.Payment.csproj", "src/Modules/Payment/ShopFlow.Modules.Payment/"]
COPY ["src/Modules/Payment/ShopFlow.Modules.Payment.Contracts/ShopFlow.Modules.Payment.Contracts.csproj", "src/Modules/Payment/ShopFlow.Modules.Payment.Contracts/"]
COPY ["src/Modules/Notification/ShopFlow.Modules.Notification/ShopFlow.Modules.Notification.csproj", "src/Modules/Notification/ShopFlow.Modules.Notification/"]
COPY ["src/Modules/Notification/ShopFlow.Modules.Notification.Contracts/ShopFlow.Modules.Notification.Contracts.csproj", "src/Modules/Notification/ShopFlow.Modules.Notification.Contracts/"]

RUN dotnet restore "src/ShopFlow.Host/ShopFlow.Host.csproj"

COPY . .
WORKDIR "/src/src/ShopFlow.Host"
RUN dotnet build "ShopFlow.Host.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "ShopFlow.Host.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

RUN groupadd -r appuser && useradd -r -g appuser appuser
USER appuser

COPY --from=publish /app/publish .

HEALTHCHECK --interval=30s --timeout=10s --start-period=10s --retries=3 \
  CMD ["dotnet", "ShopFlow.Host.dll", "--healthcheck"]

ENTRYPOINT ["dotnet", "ShopFlow.Host.dll"]
