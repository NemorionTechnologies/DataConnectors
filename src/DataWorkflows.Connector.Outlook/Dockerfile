FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["src/DataWorkflows.Connector.Outlook/DataWorkflows.Connector.Outlook.csproj", "DataWorkflows.Connector.Outlook/"]
COPY ["src/DataWorkflows.Contracts/DataWorkflows.Contracts.csproj", "DataWorkflows.Contracts/"]
RUN dotnet restore "DataWorkflows.Connector.Outlook/DataWorkflows.Connector.Outlook.csproj"
COPY src/DataWorkflows.Connector.Outlook/. DataWorkflows.Connector.Outlook/
COPY src/DataWorkflows.Contracts/. DataWorkflows.Contracts/
WORKDIR "/src/DataWorkflows.Connector.Outlook"
RUN dotnet build "DataWorkflows.Connector.Outlook.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "DataWorkflows.Connector.Outlook.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "DataWorkflows.Connector.Outlook.dll"]
