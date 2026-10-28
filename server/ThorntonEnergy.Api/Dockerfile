# ---------- Stage 1: build (SDK image has the compiler) ----------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy only the .csproj first and restore NuGet packages.
# Docker caches each step, so packages are only re-downloaded when the .csproj changes.
COPY ThorntonEnergy.Api.csproj .
RUN dotnet restore

# Now copy the rest of the source and compile a Release build into /app
COPY . .
RUN dotnet publish -c Release -o /app --no-restore

# ---------- Stage 2: run (small runtime image, no SDK) ----------
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .

# Run as the built-in non-root user, and listen on HTTP 8080 (the image's default)
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "ThorntonEnergy.Api.dll"]
