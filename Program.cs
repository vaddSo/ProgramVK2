using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace vaddso;

public class WeatherData
{
    public string City {get; set;} = "";
    public string Country {get; set;} = "";
    public double TempC {get; set;}

    public override string ToString()
    {
        string sign = TempC >= 0 ? "+" : "";
        return $"{City}, {Country} {sign}{TempC:0} C";
    }
}

internal class WttrResponse
{
    [JsonPropertyName("current_condition")]
    public List<CurrentCondition>? CurrentCondition {get; set;}

    [JsonPropertyName("nearest_area")]
    public List<NearestArea>? NearestArea {get; set;}
}

internal class CurrentCondition
{
    [JsonPropertyName("temp_C")]
    public string? TempC {get; set;}
}

internal class NearestArea
{
    [JsonPropertyName("country")]
    public List<AreaValue>? Country {get; set;}

    [JsonPropertyName("areaName")]
    public List<AreaValue>? AreaName {get; set;}
}

internal class AreaValue
{
    [JsonPropertyName("value")]
    public string? Value {get; set;}
}

internal static class Program
{
    private const string ApiTemplate = "https://wttr.in/{0}?format=j1";

    private static async Task<int> Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;

        string citiesFile = args.Length > 0 ? args[0] : "cities.txt";

        if (!File.Exists(citiesFile))
        {
            Console.Error.WriteLine($"Файл со списком городов не найден: {citiesFile}");
            return 1;
        }

        var cities = File.ReadAllLines(citiesFile).Select(l => l.Trim()).Where(l => l.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

        using var http = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(30)
        };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0");

        var results = new List<WeatherData>();

        foreach (var city in cities)
        {
            try
            {
                var data = await FetchWeatherAsync(http, city);
                if (data != null)
                {
                    results.Add(data);
                }
                else
                {
                    Console.Error.WriteLine($"Не удалось получить данные для города: {city}");
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Ошибка при запросе для {city}: {ex.Message}");
            }

            await Task.Delay(1500);
        }

        Console.WriteLine("Погода по городам");
        foreach (var w in results)
        {
            Console.WriteLine(w);
        }

        Console.WriteLine();

        Console.WriteLine("Сводка по странам");
        var byCountry = results.GroupBy(r => r.Country, StringComparer.OrdinalIgnoreCase).OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase);

        foreach (var group in byCountry)
        {
            var temps = group.Select(g => g.TempC).ToList();
            double avg = temps.Average();
            double min = temps.Min();
            double max = temps.Max();
            int count = temps.Count;

            string citiesWord = count == 1 ? "city" : "cities";

            string avgSign = avg >= 0 ? "+" : "";
            string minSign = min >= 0 ? "+" : "";
            string maxSign = max >= 0 ? "+" : "";

            Console.WriteLine(
                $"{group.Key} - {count} {citiesWord}, " + $"avg: {avgSign}{avg:0} C, " + $"min: {minSign}{min:0} C, " + $"max: {maxSign}{max:0} C");
        }
        return 0;
    }

    private static async Task<WeatherData?> FetchWeatherAsync(HttpClient http, string city)
    {
        string url = string.Format(ApiTemplate, Uri.EscapeDataString(city));
        string json = await http.GetStringAsync(url);

        var dto = JsonSerializer.Deserialize<WttrResponse>(json);
        if (dto == null || dto.CurrentCondition == null || dto.CurrentCondition.Count == 0)
            return null;

        double temp = 0;
        string? rawTemp = dto.CurrentCondition[0].TempC;
        if (!string.IsNullOrWhiteSpace(rawTemp))
        {
            double.TryParse(rawTemp, NumberStyles.Float, CultureInfo.InvariantCulture, out temp);
        }

        string country = "Unknown";
        string resolvedCity = city;

        if (dto.NearestArea != null && dto.NearestArea.Count > 0)
        {
            var area = dto.NearestArea[0];

            if (area.Country != null && area.Country.Count > 0 && area.Country[0].Value != null)
            {
                country = area.Country[0].Value;
            }

            if (area.AreaName != null && area.AreaName.Count > 0 && area.AreaName[0].Value != null)
            {
                resolvedCity = area.AreaName[0].Value;
            }
        }

        return new WeatherData
        {
            City = resolvedCity, Country = country, TempC = temp
        };
    }
}