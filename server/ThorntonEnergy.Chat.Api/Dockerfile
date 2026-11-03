FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_HTTP_PORTS=8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["ThorntonEnergy.Chat.Api/ThorntonEnergy.Chat.Api.csproj", "ThorntonEnergy.Chat.Api/"]
COPY ["ThorntonEnergy.Shared/ThorntonEnergy.Shared.csproj", "ThorntonEnergy.Shared/"]
RUN dotnet restore "ThorntonEnergy.Chat.Api/ThorntonEnergy.Chat.Api.csproj"
COPY . .
WORKDIR /src/ThorntonEnergy.Chat.Api
RUN dotnet publish "ThorntonEnergy.Chat.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
USER $APP_UID
ENTRYPOINT ["dotnet", "ThorntonEnergy.Chat.Api.dll"]
