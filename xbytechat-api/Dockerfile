# =======================
# 1. Build stage
# =======================
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy the csproj and restore as a separate layer
COPY xbytechat.api.csproj ./
RUN dotnet restore xbytechat.api.csproj

# Copy the rest of the source code
COPY . .

# Publish in Release mode
RUN dotnet publish xbytechat.api.csproj -c Release -o /app/publish --no-restore /p:UseAppHost=false

# =======================
# 2. Runtime stage
# =======================
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

COPY --from=build /app/publish ./

# Listen on port 80 inside the container
ENV ASPNETCORE_URLS=http://+:80
EXPOSE 80

ENTRYPOINT ["dotnet", "xbytechat.api.dll"]
