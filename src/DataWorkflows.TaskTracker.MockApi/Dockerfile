FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["src/DataWorkflows.TaskTracker.MockApi/DataWorkflows.TaskTracker.MockApi.csproj", "DataWorkflows.TaskTracker.MockApi/"]
RUN dotnet restore "DataWorkflows.TaskTracker.MockApi/DataWorkflows.TaskTracker.MockApi.csproj"
COPY src/DataWorkflows.TaskTracker.MockApi/. DataWorkflows.TaskTracker.MockApi/
WORKDIR "/src/DataWorkflows.TaskTracker.MockApi"
RUN dotnet build "DataWorkflows.TaskTracker.MockApi.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "DataWorkflows.TaskTracker.MockApi.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "DataWorkflows.TaskTracker.MockApi.dll"]
