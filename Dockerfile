FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["CloudCrate.Api/CloudCrate.Api.csproj", "CloudCrate.Api/"]
COPY ["CloudCrate.Application/CloudCrate.Application.csproj", "CloudCrate.Application/"]
COPY ["CloudCrate.Domain/CloudCrate.Domain.csproj", "CloudCrate.Domain/"]
COPY ["CloudCrate.Infrastructure/CloudCrate.Infrastructure.csproj", "CloudCrate.Infrastructure/"]
RUN dotnet restore "CloudCrate.Api/CloudCrate.Api.csproj"
COPY . .
WORKDIR "/src/CloudCrate.Api"
RUN dotnet build "CloudCrate.Api.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "CloudCrate.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "CloudCrate.Api.dll"]