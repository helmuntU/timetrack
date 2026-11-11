# ---- Build stage: SDK image restores, tests and publishes ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore first with only the project files: this layer is cached until a .csproj changes.
COPY global.json Directory.Build.props Directory.Packages.props TimeTrack.slnx ./
COPY src/TimeTrack.Domain/TimeTrack.Domain.csproj src/TimeTrack.Domain/
COPY src/TimeTrack.Application/TimeTrack.Application.csproj src/TimeTrack.Application/
COPY src/TimeTrack.Infrastructure/TimeTrack.Infrastructure.csproj src/TimeTrack.Infrastructure/
COPY src/TimeTrack.Api/TimeTrack.Api.csproj src/TimeTrack.Api/
RUN dotnet restore src/TimeTrack.Api/TimeTrack.Api.csproj

COPY src/ src/
RUN dotnet publish src/TimeTrack.Api/TimeTrack.Api.csproj -c Release -o /app/publish --no-restore

# ---- Runtime stage: only the ASP.NET Core runtime, running as a non-root user ----
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
USER app

ENTRYPOINT ["dotnet", "TimeTrack.Api.dll"]
