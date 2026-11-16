# Builds the app into a small image: docker build -t {{HostName}} . && docker run -p 8080:8080 -v {{HostName}}-data:/data {{HostName}}
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
# The database lives on a volume at /data, so it outlasts the container. Set Seed__Password to choose the sample
# accounts' password; without it the admin's is made up and written to the log once.
ENV ASPNETCORE_HTTP_PORTS=8080 \
    ConnectionStrings__DefaultConnection="DataSource=/data/app.db;Cache=Shared" \
    ASPNETCORE_FORWARDEDHEADERS_ENABLED=true
RUN mkdir -p /data && chown $APP_UID /data
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "{{ProjectName}}.dll"]
