# syntax=docker/dockerfile:1
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["src/ProductManagement.Api/ProductManagement.Api.csproj", "src/ProductManagement.Api/"]
RUN dotnet restore "src/ProductManagement.Api/ProductManagement.Api.csproj"
COPY . .
WORKDIR /src/src/ProductManagement.Api
RUN dotnet publish -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENV APPLY_MIGRATIONS=true
ENV ENABLE_SWAGGER=true
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "ProductManagement.Api.dll"]
