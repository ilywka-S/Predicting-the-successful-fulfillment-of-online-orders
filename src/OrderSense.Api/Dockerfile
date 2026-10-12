FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Directory.Build.props Directory.Packages.props ./
COPY src/OrderSense.Api/OrderSense.Api.csproj src/OrderSense.Api/
COPY src/OrderSense.ServiceDefaults/OrderSense.ServiceDefaults.csproj src/OrderSense.ServiceDefaults/
COPY src/OrderSense.Contracts/OrderSense.Contracts.csproj src/OrderSense.Contracts/
RUN dotnet restore src/OrderSense.Api/OrderSense.Api.csproj

COPY src/OrderSense.Api/ src/OrderSense.Api/
COPY src/OrderSense.ServiceDefaults/ src/OrderSense.ServiceDefaults/
COPY src/OrderSense.Contracts/ src/OrderSense.Contracts/
RUN dotnet publish src/OrderSense.Api/OrderSense.Api.csproj -c Release -o /app/publish --no-restore -p:OpenApiGenerateDocuments=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 8080
ENTRYPOINT ["dotnet", "OrderSense.Api.dll"]