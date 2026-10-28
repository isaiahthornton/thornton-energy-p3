# Build context is server/ (see docker-compose.yml), so both this service and the Shared library are visible.

# ---------- Stage 1: build (SDK image has the compiler) ----------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy only the .csproj files first and restore NuGet packages.
# Docker caches each step, so packages are only re-downloaded when a .csproj changes.
COPY ThorntonEnergy.Shared/ThorntonEnergy.Shared.csproj ThorntonEnergy.Shared/
COPY ThorntonEnergy.Customers.Api/ThorntonEnergy.Customers.Api.csproj ThorntonEnergy.Customers.Api/
RUN dotnet restore ThorntonEnergy.Customers.Api/ThorntonEnergy.Customers.Api.csproj

# Now copy the source of both projects and compile a Release build into /app
COPY ThorntonEnergy.Shared/ ThorntonEnergy.Shared/
COPY ThorntonEnergy.Customers.Api/ ThorntonEnergy.Customers.Api/
RUN dotnet publish ThorntonEnergy.Customers.Api/ThorntonEnergy.Customers.Api.csproj -c Release -o /app --no-restore

# ---------- Stage 2: run (small runtime image, no SDK) ----------
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .

# Run as the built-in non-root user, and listen on HTTP 8080 (the image's default)
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "ThorntonEnergy.Customers.Api.dll"]
