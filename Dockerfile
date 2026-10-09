# ---------- 1) Compilar ----------
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY VicdogsPeru.csproj ./
RUN dotnet restore VicdogsPeru.csproj
COPY . .
RUN dotnet publish VicdogsPeru.csproj -c Release -o /app/publish --no-restore

# ---------- 2) Ejecutar ----------
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

# Fuentes: ClosedXML las necesita para ajustar el ancho de columnas al exportar a Excel
RUN apt-get update \
 && apt-get install -y --no-install-recommends fontconfig fonts-liberation fonts-crosextra-carlito \
 && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .
COPY docker-entrypoint.sh /docker-entrypoint.sh
RUN sed -i 's/\r$//' /docker-entrypoint.sh && chmod +x /docker-entrypoint.sh

# Hora de Perú: el contenedor está en UTC y la app usa DateTime.Today (vacunas, recordatorios)
ENV TZ=America/Lima \
    ASPNETCORE_ENVIRONMENT=Production \
    ASPNETCORE_URLS=http://0.0.0.0:10000 \
    DOTNET_RUNNING_IN_CONTAINER=true

EXPOSE 10000
ENTRYPOINT ["/docker-entrypoint.sh"]
