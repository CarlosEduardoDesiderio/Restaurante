FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY Restaurante.Api/Restaurante.Api.csproj Restaurante.Api/
RUN dotnet restore Restaurante.Api/Restaurante.Api.csproj
COPY Restaurante.Api/ Restaurante.Api/
RUN dotnet publish Restaurante.Api/Restaurante.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://0.0.0.0:10000
EXPOSE 10000
ENTRYPOINT ["dotnet", "Restaurante.Api.dll"]
