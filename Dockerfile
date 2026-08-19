# syntax=docker/dockerfile:1
FROM mcr.microsoft.com/dotnet/sdk:10.0.302-noble AS development
WORKDIR /workspace

FROM development AS build
COPY . .
RUN dotnet restore TradingBot.sln
RUN dotnet build TradingBot.sln --configuration Release --no-restore
RUN dotnet publish src/Trading.Host/Trading.Host.csproj --configuration Release --no-restore --output /app

FROM build AS test
RUN dotnet test TradingBot.sln --configuration Release --no-build

FROM mcr.microsoft.com/dotnet/runtime:10.0.10-noble AS headless-runtime
WORKDIR /app
COPY --from=build /app .
VOLUME ["/data"]
ENTRYPOINT ["dotnet", "Trading.Host.dll"]
