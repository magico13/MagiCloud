# This dockerfile is for building an image from the contents 
# of the zip file produced by Jenkins

FROM mcr.microsoft.com/dotnet/aspnet:6.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

COPY . .
ENTRYPOINT ["dotnet", "MagiCloud.dll"]