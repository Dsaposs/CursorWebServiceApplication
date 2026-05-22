FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY src/NotesApi/NotesApi.csproj ./NotesApi/
RUN dotnet restore ./NotesApi/NotesApi.csproj

COPY src/ ./
RUN dotnet publish ./NotesApi/NotesApi.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 5294
ENV ASPNETCORE_URLS=http://+:5294
ENTRYPOINT ["dotnet", "NotesApi.dll"]
