FROM mcr.microsoft.com/dotnet/runtime:6.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
ARG BUILD_CONFIGURATION=Release
ENV instanceId=1
WORKDIR /src
COPY ["DockerTools.Valkyrie/DockerTools.Valkyrie.csproj", "DockerTools.Valkyrie/"]
RUN dotnet restore "DockerTools.Valkyrie/DockerTools.Valkyrie.csproj"
COPY . .
WORKDIR "/src/DockerTools.Valkyrie"
RUN dotnet build "DockerTools.Valkyrie.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "DockerTools.Valkyrie.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Kenbi.DockerTools.Valkyrie.dll"]
CMD InstanceId $instanceId
