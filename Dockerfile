FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY GMSoft.slnx ./
COPY GMSoft.API/GMSoft.API.csproj GMSoft.API/
COPY GMSoft.Application/GMSoft.Application.csproj GMSoft.Application/
COPY GMSoft.Data/GMSoft.Data.csproj GMSoft.Data/
COPY GMSoft.Domain/GMSoft.Domain.csproj GMSoft.Domain/
RUN dotnet restore GMSoft.API/GMSoft.API.csproj
COPY . .
RUN dotnet publish GMSoft.API/GMSoft.API.csproj -c Release --no-restore -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build --chown=app:app /app/publish .
# Npgsql busca la librería de Kerberos al conectar; sin ella deja un error en el log.
RUN apt-get update && apt-get install -y --no-install-recommends libgssapi-krb5-2 \
    && rm -rf /var/lib/apt/lists/* \
    && mkdir /app/logs && chown app:app /app/logs
USER app
EXPOSE 8080
ENTRYPOINT ["sh", "-c", "ASPNETCORE_URLS=http://0.0.0.0:${PORT:-8080} exec dotnet GMSoft.API.dll"]
