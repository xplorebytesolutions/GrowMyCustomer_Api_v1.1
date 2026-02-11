# =======================
# 1. Build stage
# =======================
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Update: Copy the file using the folder path
COPY xbytechat-api/xbytechat.api.csproj xbytechat-api/
RUN dotnet restore xbytechat-api/xbytechat.api.csproj

# Copy everything else
COPY . .

# Update: Publish from the specific folder
WORKDIR /src/xbytechat-api
RUN dotnet publish xbytechat.api.csproj -c Release -o /app/publish --no-restore /p:UseAppHost=false

# =======================
# 2. Runtime stage
# =======================
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

COPY --from=build /app/publish ./

# Optional: Set timezone to India (Good for logs)
ENV TZ=Asia/Kolkata
RUN ln -snf /usr/share/zoneinfo/$TZ /etc/localtime && echo $TZ > /etc/timezone

# Listen on port 3298
ENV ASPNETCORE_URLS=http://+:3298
EXPOSE 3298

ENTRYPOINT ["dotnet", "xbytechat.api.dll"]
