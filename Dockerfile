FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY src/SecretSanta/SecretSanta.csproj src/SecretSanta/
# ReadyToRun: the app, EF Core and Npgsql are compiled ahead of time here, so the 0.1 CPU instance does not spend
# its startup in the JIT (measured at 0.1 CPU: about 13 s down to about 7 s).
RUN dotnet restore src/SecretSanta/SecretSanta.csproj -r linux-x64 -p:PublishReadyToRun=true
COPY src/SecretSanta/ src/SecretSanta/
RUN dotnet publish src/SecretSanta/SecretSanta.csproj -c Release -r linux-x64 --self-contained false -p:PublishReadyToRun=true -o /app --no-restore

# Ubuntu 24.04 image: it ships tzdata, which Fmt.Local needs for Asia/Yerevan (CI checks this).
FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
USER app
ENTRYPOINT ["dotnet", "SecretSanta.dll"]
