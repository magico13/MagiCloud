# This dockerfile is for building an image from the contents 
# of the zip file produced by Jenkins

FROM mcr.microsoft.com/dotnet/aspnet:6.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

RUN apt-get update && apt-get install -y libgif7 libjpeg62 libopenjp2-7 libpng16-16 libtiff5 libwebp6

COPY . .
ENTRYPOINT ["dotnet", "MagiCloud.dll"]