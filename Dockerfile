# ---------- build ----------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY global.json Directory.Build.props ./
COPY src/BankCore.Domain/BankCore.Domain.csproj src/BankCore.Domain/
COPY src/BankCore.Application/BankCore.Application.csproj src/BankCore.Application/
COPY src/BankCore.Infrastructure/BankCore.Infrastructure.csproj src/BankCore.Infrastructure/
COPY src/BankCore.Api/BankCore.Api.csproj src/BankCore.Api/
RUN dotnet restore src/BankCore.Api/BankCore.Api.csproj

COPY src/ src/
RUN dotnet publish src/BankCore.Api/BankCore.Api.csproj -c Release -o /app/publish --no-restore

# ---------- runtime ----------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

# Run as the non-root user that ships with the official image.
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "BankCore.Api.dll"]
