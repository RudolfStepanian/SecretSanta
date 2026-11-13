FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY src/SecretSanta/SecretSanta.csproj src/SecretSanta/
RUN dotnet restore src/SecretSanta/SecretSanta.csproj
COPY src/SecretSanta/ src/SecretSanta/
RUN dotnet publish src/SecretSanta/SecretSanta.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
USER app
ENTRYPOINT ["dotnet", "SecretSanta.dll"]
