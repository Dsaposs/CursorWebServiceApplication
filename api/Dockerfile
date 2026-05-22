FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY src/NotesApi/NotesApi.csproj ./NotesApi/
RUN dotnet restore ./NotesApi/NotesApi.csproj

COPY src/ ./
RUN dotnet publish ./NotesApi/NotesApi.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
RUN apt-get update && apt-get install -y --no-install-recommends wget && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 5294
ENV ASPNETCORE_URLS=http://+:5294
ENTRYPOINT ["dotnet", "NotesApi.dll"]
