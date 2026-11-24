FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Saas.Billing.slnx ./
COPY src/Saas.Billing.Domain/Saas.Billing.Domain.csproj src/Saas.Billing.Domain/
COPY src/Saas.Billing.Application/Saas.Billing.Application.csproj src/Saas.Billing.Application/
COPY src/Saas.Billing.Infrastructure/Saas.Billing.Infrastructure.csproj src/Saas.Billing.Infrastructure/
COPY src/Saas.Billing.Api/Saas.Billing.Api.csproj src/Saas.Billing.Api/
COPY tests/Saas.Billing.UnitTests/Saas.Billing.UnitTests.csproj tests/Saas.Billing.UnitTests/
COPY tests/Saas.Billing.IntegrationTests/Saas.Billing.IntegrationTests.csproj tests/Saas.Billing.IntegrationTests/

RUN dotnet restore Saas.Billing.slnx

COPY src/ src/
COPY tests/ tests/

RUN dotnet build Saas.Billing.slnx -c Release --no-restore

FROM build AS test
RUN dotnet test tests/Saas.Billing.UnitTests/Saas.Billing.UnitTests.csproj -c Release --no-build --verbosity normal

FROM build AS publish
RUN dotnet publish src/Saas.Billing.Api/Saas.Billing.Api.csproj -c Release --no-restore -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

ENV ASPNETCORE_HTTP_PORTS=
EXPOSE 8080

COPY --from=publish /app/publish .
USER $APP_UID

ENTRYPOINT ["dotnet", "Saas.Billing.Api.dll"]
