# Smart Bikestation – Demo ohne Hardware (API, Dashboard und virtuelle Station in einem Container)
#   docker compose up --build   ->   http://localhost:8080

# 1. Dashboard bauen (React + Vite)
FROM node:22-alpine AS frontend
WORKDIR /src
COPY Frontend/api/package.json Frontend/api/package-lock.json ./
RUN npm ci --no-fund --no-audit
COPY Frontend/api/ ./
RUN npm run build

# 2. API bauen (ASP.NET Core 10)
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS backend
WORKDIR /src
COPY api/bikestation/bikestation/bikestation.csproj ./
RUN dotnet restore bikestation.csproj
COPY api/bikestation/bikestation/ ./
RUN dotnet publish bikestation.csproj -c Release -o /app --no-restore

# 3. Laufzeit: API liefert das Dashboard aus wwwroot mit aus
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=backend /app ./
COPY --from=frontend /src/dist ./wwwroot
RUN mkdir /data && chown $APP_UID /data
USER $APP_UID
ENV ASPNETCORE_ENVIRONMENT=Demo \
    ASPNETCORE_URLS=http://+:8080 \
    ConnectionStrings__Bikestation="Data Source=/data/bikestation-demo.db"
EXPOSE 8080
ENTRYPOINT ["dotnet", "bikestation.dll"]
