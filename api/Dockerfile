# api/Dockerfile — built with the solution root as context (see docker-compose.yml)
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY api/api.csproj api/
RUN dotnet restore api/api.csproj
COPY . .
RUN dotnet publish api/api.csproj -c Release -o /app/publish --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

# Tesseract OCRs scanned PDFs in the knowledge base. Only the Linux program comes from the distro. The language
# models are published with the app from services/Tesseract/Tessdata/ (Tesseract:TessdataPath).
RUN apt-get update \
    && apt-get install -y --no-install-recommends tesseract-ocr \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .
RUN mkdir -p /app/app_dbs /app/app_files && chown -R $APP_UID /app/app_dbs /app/app_files
USER $APP_UID
ENV ASPNETCORE_HTTP_PORTS=5000
ENV TZ=Europe/Copenhagen
EXPOSE 5000
ENTRYPOINT ["dotnet", "api.dll"]
