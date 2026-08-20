FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY simpleXflow.slnx ./
COPY src/SimpleXflow.Domain/SimpleXflow.Domain.csproj src/SimpleXflow.Domain/
COPY src/SimpleXflow.Application/SimpleXflow.Application.csproj src/SimpleXflow.Application/
COPY src/SimpleXflow.Infrastructure/SimpleXflow.Infrastructure.csproj src/SimpleXflow.Infrastructure/
COPY src/SimpleXflow.Web/SimpleXflow.Web.csproj src/SimpleXflow.Web/
COPY tests/SimpleXflow.Domain.Tests/SimpleXflow.Domain.Tests.csproj tests/SimpleXflow.Domain.Tests/
COPY tests/SimpleXflow.Infrastructure.Tests/SimpleXflow.Infrastructure.Tests.csproj tests/SimpleXflow.Infrastructure.Tests/

RUN dotnet restore simpleXflow.slnx

COPY . .
RUN dotnet publish src/SimpleXflow.Web/SimpleXflow.Web.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish \
    -p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production
ENV DataProtection__KeyPath=/tmp/simplexflow/DataProtectionKeys

EXPOSE 8080

COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "SimpleXflow.Web.dll"]
