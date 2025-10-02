FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["src/DataWorkflows.Connector.TaskTracker/DataWorkflows.Connector.TaskTracker.csproj", "DataWorkflows.Connector.TaskTracker/"]
COPY ["src/DataWorkflows.Contracts/DataWorkflows.Contracts.csproj", "DataWorkflows.Contracts/"]
RUN dotnet restore "DataWorkflows.Connector.TaskTracker/DataWorkflows.Connector.TaskTracker.csproj"
COPY src/DataWorkflows.Connector.TaskTracker/. DataWorkflows.Connector.TaskTracker/
COPY src/DataWorkflows.Contracts/. DataWorkflows.Contracts/
WORKDIR "/src/DataWorkflows.Connector.TaskTracker"
RUN dotnet build "DataWorkflows.Connector.TaskTracker.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "DataWorkflows.Connector.TaskTracker.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "DataWorkflows.Connector.TaskTracker.dll"]
