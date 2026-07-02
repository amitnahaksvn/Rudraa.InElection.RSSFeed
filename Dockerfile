# Builds either service from this one Dockerfile:
#   docker build --build-arg PROJECT=Worker -t politicalnews-worker .
#   docker build --build-arg PROJECT=Web    -t politicalnews-web    .
ARG PROJECT=Worker

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG PROJECT
WORKDIR /src

COPY Rudraa.InElection.RSSFeed.slnx ./
COPY src/Domain/Domain.csproj src/Domain/
COPY src/Application/Application.csproj src/Application/
COPY src/Infrastructure/Infrastructure.csproj src/Infrastructure/
COPY src/ServiceDefaults/ServiceDefaults.csproj src/ServiceDefaults/
COPY src/Worker/Worker.csproj src/Worker/
COPY src/Web/Web.csproj src/Web/
RUN dotnet restore "src/${PROJECT}/${PROJECT}.csproj"

COPY src/ src/
RUN dotnet publish "src/${PROJECT}/${PROJECT}.csproj" -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app .

ARG PROJECT
ENV DOTNET_EnableDiagnostics=0
ENV PROJECT_DLL="${PROJECT}.dll"
ENTRYPOINT ["sh", "-c", "dotnet ${PROJECT_DLL}"]
