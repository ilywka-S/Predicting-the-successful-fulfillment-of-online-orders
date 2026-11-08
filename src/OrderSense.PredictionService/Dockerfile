FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Directory.Build.props Directory.Packages.props ./
COPY src/OrderSense.PredictionService/OrderSense.PredictionService.csproj src/OrderSense.PredictionService/
COPY src/OrderSense.ServiceDefaults/OrderSense.ServiceDefaults.csproj src/OrderSense.ServiceDefaults/
COPY src/OrderSense.Contracts/OrderSense.Contracts.csproj src/OrderSense.Contracts/
RUN dotnet restore src/OrderSense.PredictionService/OrderSense.PredictionService.csproj

COPY src/OrderSense.PredictionService/ src/OrderSense.PredictionService/
COPY src/OrderSense.ServiceDefaults/ src/OrderSense.ServiceDefaults/
COPY src/OrderSense.Contracts/ src/OrderSense.Contracts/
COPY ml/models/ ml/models/
RUN dotnet publish src/OrderSense.PredictionService/OrderSense.PredictionService.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 8080
ENTRYPOINT ["dotnet", "OrderSense.PredictionService.dll"]