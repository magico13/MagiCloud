# This dockerfile is for building an image from the contents 
# of the zip file produced by Jenkins

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base

EXPOSE 8080

RUN apt-get update && \
	apt-get install -y curl libc6-dev libgdiplus libgif7 libjpeg62 libleptonica-dev libopenjp2-7 libpng16-16  libtesseract-dev && \
	rm -rf /var/lib/apt/lists/*

WORKDIR /app

COPY . .

WORKDIR /app/x64
RUN ln -s /usr/lib/x86_64-linux-gnu/liblept.so.5 liblept.so.5 && \
	ln -s /usr/lib/x86_64-linux-gnu/libleptonica.so libleptonica-1.80.0.so && \
	ln -s /usr/lib/x86_64-linux-gnu/libtesseract.so libtesseract41.so

WORKDIR /app
ENTRYPOINT ["dotnet", "GogglesApi.dll"]