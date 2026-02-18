# -------- build stage --------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy project files
COPY api/api.csproj api/
COPY dataaccess/dataaccess.csproj dataaccess/

# Restore
RUN dotnet restore api/api.csproj

# Copy everything
COPY . .

# Publish
RUN dotnet publish api/api.csproj -c Release -o /app/publish /p:UseAppHost=false

# -------- runtime stage --------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

COPY --from=build /app/publish ./
ENTRYPOINT ["dotnet", "api.dll"]
