# This dockerfile is for building an image from the contents 
# of the zip file produced by Jenkins
#
# NOTE: This image does NOT include Tesseract OCR. Image OCR is only available
# when Azure OCR is configured (see Goggles/ServiceCollectionExtensions.cs).
# Without it, image uploads are accepted but no text is extracted from them.
# To re-enable local Tesseract OCR, add the native libtesseract/libleptonica
# libraries and their soname symlinks back into this image.

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base

EXPOSE 8080

RUN apt-get update && \
    apt-get install -y curl && \
    rm -rf /var/lib/apt/lists/*

WORKDIR /app

COPY . .

# Healthcheck: /api/status returns {"status":"Ok",...} when the service (and its
# OCR engine) is healthy. Compose overrides this with its own healthcheck, so this
# mainly benefits standalone `docker run` usage.
HEALTHCHECK --interval=30s --timeout=5s --start-period=15s --retries=3 \
    CMD curl -fsS http://localhost:8080/api/status | grep -q '{"status":"Ok"' || exit 1

ENTRYPOINT ["dotnet", "GogglesApi.dll"]