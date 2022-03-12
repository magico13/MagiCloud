# This dockerfile is for building an image from the contents 
# of the zip file produced by Jenkins

FROM mcr.microsoft.com/dotnet/aspnet:6.0 AS base

EXPOSE 80
EXPOSE 443

RUN apt-get update && apt-get install libgif7 libjpeg62 libopenjp2-7 libpng16-16 libtiff5 libwebp6 libc6-dev libgdiplus libleptonica-dev libtesseract-dev -y

WORKDIR /app

COPY . .

WORKDIR /app/x64
RUN ln -s /usr/lib/x86_64-linux-gnu/liblept.so.5 liblept.so.5
RUN ln -s /usr/lib/x86_64-linux-gnu/libleptonica.so libleptonica-1.80.0.so
RUN ln -s /usr/lib/x86_64-linux-gnu/libtesseract.so libtesseract41.so

WORKDIR /app
ENTRYPOINT ["dotnet", "MagiCloud.dll"]