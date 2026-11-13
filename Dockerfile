FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY src/SecretSatana/SecretSatana.csproj src/SecretSatana/
RUN dotnet restore src/SecretSatana/SecretSatana.csproj
COPY src/SecretSatana/ src/SecretSatana/
RUN dotnet publish src/SecretSatana/SecretSatana.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
USER app
ENTRYPOINT ["dotnet", "SecretSatana.dll"]
