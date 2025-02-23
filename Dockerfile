# This dockerfile is for building an image from the contents 
# of the zip file produced by Jenkins

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base

EXPOSE 80
EXPOSE 443

WORKDIR /app

COPY publish-magicloud/* .

ENTRYPOINT ["dotnet", "MagiCloud.dll"]