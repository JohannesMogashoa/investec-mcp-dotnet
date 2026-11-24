FROM mcr.microsoft.com/dotnet/sdk:11.0 AS build
WORKDIR /src
COPY global.json Directory.Build.props InvestecMcp.slnx ./
COPY src/InvestecMcp/InvestecMcp.csproj src/InvestecMcp/packages.lock.json src/InvestecMcp/
RUN dotnet restore src/InvestecMcp/InvestecMcp.csproj --locked-mode
COPY src/InvestecMcp/ src/InvestecMcp/
RUN dotnet publish src/InvestecMcp/InvestecMcp.csproj -c Release --no-restore -o /app

FROM build AS protocol-test-build
COPY tests/InvestecMcp.Tests/InvestecMcp.Tests.csproj tests/InvestecMcp.Tests/packages.lock.json tests/InvestecMcp.Tests/
RUN dotnet restore tests/InvestecMcp.Tests/InvestecMcp.Tests.csproj --locked-mode
COPY tests/InvestecMcp.Tests/ tests/InvestecMcp.Tests/
RUN dotnet publish tests/InvestecMcp.Tests/InvestecMcp.Tests.csproj -c Release --no-restore -o /test-app

FROM mcr.microsoft.com/dotnet/runtime:11.0 AS protocol-test
WORKDIR /app
COPY --from=protocol-test-build /test-app/ ./
USER $APP_UID
ENTRYPOINT ["dotnet", "InvestecMcp.Tests.dll", "--banking-test"]

FROM mcr.microsoft.com/dotnet/runtime:11.0 AS production
WORKDIR /app
COPY --from=build /app/ ./
USER $APP_UID
ENTRYPOINT ["dotnet", "InvestecMcp.dll"]
