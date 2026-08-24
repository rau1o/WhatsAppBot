using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace WhatsAppBot.AdminPanel.Services;

public record GeocodeResult(double Latitude, double Longitude, string DisplayName);

// La búsqueda corre del lado del servidor (Blazor Server), no desde el
// browser — evita problemas de CORS y cumple la política de uso de
// Nominatim, que pide identificar la aplicación en el User-Agent.
public class GeocodingService
{
    private readonly HttpClient _http;

    public GeocodingService(IHttpClientFactory factory)
    {
        _http = factory.CreateClient("Nominatim");
    }

    public async Task<List<GeocodeResult>> SearchAsync(string query, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(query)) return [];

        var url = $"search?format=json&limit=5&q={Uri.EscapeDataString(query)}";

        try
        {
            var results = await _http.GetFromJsonAsync<List<NominatimResult>>(url, ct) ?? [];

            return results
                .Select(r => new GeocodeResult(
                    double.Parse(r.Lat, CultureInfo.InvariantCulture),
                    double.Parse(r.Lon, CultureInfo.InvariantCulture),
                    r.DisplayName))
                .ToList();
        }
        catch (HttpRequestException)
        {
            return [];
        }
    }

    private record NominatimResult(
        [property: JsonPropertyName("lat")] string Lat,
        [property: JsonPropertyName("lon")] string Lon,
        [property: JsonPropertyName("display_name")] string DisplayName);
}
