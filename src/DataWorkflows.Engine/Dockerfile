FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["src/DataWorkflows.Engine/DataWorkflows.Engine.csproj", "DataWorkflows.Engine/"]
COPY ["src/DataWorkflows.Contracts/DataWorkflows.Contracts.csproj", "DataWorkflows.Contracts/"]
COPY ["src/DataWorkflows.Data/DataWorkflows.Data.csproj", "DataWorkflows.Data/"]
RUN dotnet restore "DataWorkflows.Engine/DataWorkflows.Engine.csproj"
COPY src/DataWorkflows.Engine/. DataWorkflows.Engine/
COPY src/DataWorkflows.Contracts/. DataWorkflows.Contracts/
COPY src/DataWorkflows.Data/. DataWorkflows.Data/
WORKDIR "/src/DataWorkflows.Engine"
RUN dotnet build "DataWorkflows.Engine.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "DataWorkflows.Engine.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "DataWorkflows.Engine.dll"]
