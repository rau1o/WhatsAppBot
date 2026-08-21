FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY . .

RUN dotnet restore "src/WhatsAppBot.AdminPanel/WhatsAppBot.AdminPanel.csproj"
RUN dotnet publish "src/WhatsAppBot.AdminPanel/WhatsAppBot.AdminPanel.csproj" -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 8080

CMD ["/bin/sh", "-c", "ASPNETCORE_URLS=http://+:${PORT:-8080} dotnet WhatsAppBot.AdminPanel.dll"]
