FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copiamos toda la solución (no solo la carpeta del Api) porque este proyecto
# referencia Domain/Application/Infrastructure via ProjectReference — con un
# build context acotado a src/WhatsAppBot.Api, esas referencias no resuelven.
COPY . .

RUN dotnet restore "src/WhatsAppBot.Api/WhatsAppBot.Api.csproj"
RUN dotnet publish "src/WhatsAppBot.Api/WhatsAppBot.Api.csproj" -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 8080

# Railway inyecta $PORT dinámicamente en cada deploy — Kestrel tiene que
# escuchar ahí, no en un puerto fijo hardcodeado.
CMD ["/bin/sh", "-c", "ASPNETCORE_URLS=http://+:${PORT:-8080} dotnet WhatsAppBot.Api.dll"]
