FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["src/DataWorkflows.Engine.Api/DataWorkflows.Engine.Api.csproj", "DataWorkflows.Engine.Api/"]
COPY ["src/DataWorkflows.Engine.Core/DataWorkflows.Engine.Core.csproj", "DataWorkflows.Engine.Core/"]
COPY ["src/DataWorkflows.Engine.Infrastructure/DataWorkflows.Engine.Infrastructure.csproj", "DataWorkflows.Engine.Infrastructure/"]
COPY ["src/DataWorkflows.Contracts/DataWorkflows.Contracts.csproj", "DataWorkflows.Contracts/"]
COPY ["src/DataWorkflows.Data/DataWorkflows.Data.csproj", "DataWorkflows.Data/"]
RUN dotnet restore "DataWorkflows.Engine.Api/DataWorkflows.Engine.Api.csproj"
COPY src/DataWorkflows.Engine.Api/. DataWorkflows.Engine.Api/
COPY src/DataWorkflows.Engine.Core/. DataWorkflows.Engine.Core/
COPY src/DataWorkflows.Engine.Infrastructure/. DataWorkflows.Engine.Infrastructure/
COPY src/DataWorkflows.Contracts/. DataWorkflows.Contracts/
COPY src/DataWorkflows.Data/. DataWorkflows.Data/
WORKDIR "/src/DataWorkflows.Engine.Api"
RUN dotnet build "DataWorkflows.Engine.Api.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "DataWorkflows.Engine.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "DataWorkflows.Engine.Api.dll"]
