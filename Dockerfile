FROM mcr.microsoft.com/dotnet/sdk:9.0 AS builder
WORKDIR /src

COPY K8sGateway.sln .
COPY src/K8sGateway.Core/K8sGateway.Core.csproj src/K8sGateway.Core/
COPY src/K8sGateway.Infrastructure/K8sGateway.Infrastructure.csproj src/K8sGateway.Infrastructure/
COPY src/K8sGateway.Host/K8sGateway.Host.csproj src/K8sGateway.Host/

RUN dotnet restore K8sGateway.sln
COPY src/ src/

RUN dotnet build K8sGateway.sln -c Release --no-restore
RUN dotnet publish src/K8sGateway.Host/K8sGateway.Host.csproj -c Release --no-build -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:9.0-noble-chiseled
WORKDIR /app

COPY --from=builder /app/publish .

RUN useradd -m -u 1001 dotnetuser && \
    chown -R dotnetuser:dotnetuser /app

USER dotnetuser
EXPOSE 3000

ENV ASPNETCORE_URLS=http://+:3000
ENTRYPOINT ["dotnet", "K8sGateway.Host.dll"]
