# The docs site (site/IzzyBlazor.Site), for Fly.io. Built with the .NET 10 SDK and Node 22 (the
# library's build runs npm ci and esbuild for its CSS and scripts), then run on the smaller
# ASP.NET Core runtime image.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
COPY --from=node:22-bookworm-slim /usr/local/ /usr/local/
WORKDIR /src
COPY . .
RUN dotnet publish site/IzzyBlazor.Site/IzzyBlazor.Site.csproj -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "IzzyBlazor.Site.dll"]
