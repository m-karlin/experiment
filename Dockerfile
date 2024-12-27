FROM mcr.microsoft.com/dotnet/sdk:8.0-cbl-mariner2.0 AS build
WORKDIR /app

COPY *.csproj ./
COPY *.props ./
COPY *.json ./
RUN dotnet restore

COPY . ./
RUN dotnet publish -c Release -o out

FROM mcr.microsoft.com/dotnet/aspnet:8.0.8-noble-chiseled-extra
WORKDIR /app
COPY --from=build /app/out ./

EXPOSE 80

ENTRYPOINT ["dotnet", "Sandbox.dll"]
