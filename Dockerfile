# Builds the single Web service - it owns both the HTTP API and Hangfire job execution (RSS/API
# crawl schedules). There is no separate Worker project anymore (retired so this app fits a
# free-tier host with no paid background-worker service required), so this Dockerfile no longer
# needs the old --build-arg PROJECT=Worker|Web switch - it always builds Web.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY src/Domain/Domain.csproj src/Domain/
COPY src/Application/Application.csproj src/Application/
COPY src/Infrastructure/Infrastructure.csproj src/Infrastructure/
COPY src/ServiceDefaults/ServiceDefaults.csproj src/ServiceDefaults/
COPY src/Web/Web.csproj src/Web/
RUN dotnet restore "src/Web/Web.csproj"

COPY src/ src/
RUN dotnet publish "src/Web/Web.csproj" -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app .

ENV DOTNET_EnableDiagnostics=0
ENTRYPOINT ["dotnet", "Web.dll"]
