FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY NotesApi/NotesApi.csproj NotesApi/
RUN dotnet restore NotesApi/NotesApi.csproj

COPY NotesApi/ NotesApi/
WORKDIR /src/NotesApi
RUN dotnet publish NotesApi.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

EXPOSE 8080
RUN mkdir -p /data

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "NotesApi.dll"]
