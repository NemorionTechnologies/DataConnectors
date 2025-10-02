FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["src/DataWorkflows.Connector.Confluence/DataWorkflows.Connector.Confluence.csproj", "DataWorkflows.Connector.Confluence/"]
COPY ["src/DataWorkflows.Contracts/DataWorkflows.Contracts.csproj", "DataWorkflows.Contracts/"]
RUN dotnet restore "DataWorkflows.Connector.Confluence/DataWorkflows.Connector.Confluence.csproj"
COPY src/DataWorkflows.Connector.Confluence/. DataWorkflows.Connector.Confluence/
COPY src/DataWorkflows.Contracts/. DataWorkflows.Contracts/
WORKDIR "/src/DataWorkflows.Connector.Confluence"
RUN dotnet build "DataWorkflows.Connector.Confluence.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "DataWorkflows.Connector.Confluence.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "DataWorkflows.Connector.Confluence.dll"]
