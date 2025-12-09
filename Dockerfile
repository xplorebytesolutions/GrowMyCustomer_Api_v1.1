FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY xbytechat-api/xbytechat.api.csproj xbytechat-api/
RUN dotnet restore xbytechat-api/xbytechat.api.csproj
COPY . .
RUN dotnet publish xbytechat-api/xbytechat.api.csproj -c Release -o /app/publish --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish ./
ENV ASPNETCORE_URLS=http://+:80
EXPOSE 80
ENTRYPOINT ["dotnet", "xbytechat.api.dll"]
