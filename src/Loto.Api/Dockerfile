FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["LotoHelper.sln", "./"]
COPY ["src/Loto.Api/Loto.Api.csproj", "src/Loto.Api/"]
COPY ["src/Loto.Ingestion.Worker/Loto.Ingestion.Worker.csproj", "src/Loto.Ingestion.Worker/"]
COPY ["src/Loto.Infrastructure/Loto.Infrastructure.csproj", "src/Loto.Infrastructure/"]
COPY ["src/Loto.Domain/Loto.Domain.csproj", "src/Loto.Domain/"]

RUN dotnet restore "src/Loto.Api/Loto.Api.csproj"

COPY . .
RUN dotnet publish "src/Loto.Api/Loto.Api.csproj" -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Loto.Api.dll"]
