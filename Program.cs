using System;
using System.Net.Http;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using Spectre.Console;

class Program
{
    private const string pirateApiKey = "SW9SNxUlr8iFMGAjqdm50kFF8Gob1NGv";
    private const string geocodingApiKey = "e88ac4e24d494c52986589e960b86db3";

    private static readonly string[,] customSummaries = {
        { "Clear", "NO HAY NUBES!! Problablemete hay calor" },
        { "Partly Cloudy", "Medio Nublososo" },
        { "Cloudy", "Nublososo" },
        { "Rain", "Lluvia, se va a caer el techo de lamina" },
        { "Snow", "NEVANDO?? Ah.. no elegiste Tijuana lol" },
        { "Wind", "Hace VIENTOOOO AHHHH" },
        { "Fog", "Nieblina, wtf silent hill" }
    };

    private static readonly Dictionary<string, List<string>> majorCities = new Dictionary<string, List<string>>
    {
        { "rusia", new List<string> { "Moscú", "San Petersburgo", "Novosibirsk", "Ekaterimburgo", "Kazán" } },
        { "estados unidos", new List<string> { "Nueva York", "Los Ángeles", "Chicago", "Houston", "Phoenix" } },
        { "mexico", new List<string> { "Ciudad de México", "Guadalajara", "Monterrey", "Puebla", "Tijuana" } },
        // Add more countries and their major cities as needed
    };

    static async Task Main(string[] args)
    {
        DisplayHeader();

        var city = await GetCityInput();

        await AnsiConsole.Status()
            .StartAsync("Obteniendo datos del clima...", async ctx =>
            {
                var (latitude, longitude) = await GetCoordinatesAsync(city);

                if (latitude == null || longitude == null)
                {
                    DisplayError("No se pudieron obtener las coordenadas para la ciudad proporcionada.");
                    return;
                }

                string weatherData = await GetWeatherDataAsync(latitude.Value, longitude.Value);

                if (!string.IsNullOrEmpty(weatherData))
                {
                    ParseAndDisplayWeather(weatherData, city);
                    ParseAndDisplayForecast(weatherData);
                }
            });

        Console.WriteLine("\nPresione Enter para salir...");
        Console.ReadLine();
    }

    static async Task<string> GetCityInput()
    {
        while (true)
        {
            var cityInput = AnsiConsole.Prompt(
                new TextPrompt<string>("[green]Ingrese la Ciudad o País:[/]")
                    .DefaultValue("Tijuana")
                    .PromptStyle("cyan")
            );

            var lowercaseInput = cityInput.ToLower();
            if (majorCities.ContainsKey(lowercaseInput))
            {
                var cities = majorCities[lowercaseInput];
                var selection = AnsiConsole.Prompt(
                    new SelectionPrompt<string>()
                        .Title($"Seleccione una ciudad principal de {cityInput}:")
                        .AddChoices(cities)
                );
                return selection;
            }

            var locations = await GetLocationOptionsAsync(cityInput);

            if (locations.Count == 0)
            {
                DisplayError("No se encontraron ubicaciones. Por favor, intente de nuevo.");
                continue;
            }

            if (locations.Count == 1)
            {
                return locations[0].FormattedName;
            }

            var locationSelection = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("Múltiples ubicaciones encontradas. Por favor, seleccione una:")
                    .AddChoices(locations.Select(l => l.FormattedName))
            );

            return locationSelection;
        }
    }

    static async Task<List<LocationOption>> GetLocationOptionsAsync(string query)
    {
        string apiUrl = $"https://api.opencagedata.com/geocode/v1/json?q={Uri.EscapeDataString(query)}&key={geocodingApiKey}&limit=5";

        using (HttpClient client = new HttpClient())
        {
            try
            {
                HttpResponseMessage response = await client.GetAsync(apiUrl);
                response.EnsureSuccessStatusCode();
                string responseBody = await response.Content.ReadAsStringAsync();
                JObject json = JObject.Parse(responseBody);

                var results = json["results"] as JArray;
                if (results != null)
                {
                    return results.Select(r => new LocationOption
                    {
                        FormattedName = r["formatted"].Value<string>(),
                        Latitude = r["geometry"]["lat"].Value<double>(),
                        Longitude = r["geometry"]["lng"].Value<double>()
                    }).ToList();
                }

                return new List<LocationOption>();
            }
            catch (Exception e)
            {
                DisplayError($"Error al obtener opciones de ubicación: {e.Message}");
                return new List<LocationOption>();
            }
        }
    }

    static async Task<string> GetWeatherDataAsync(double latitude, double longitude)
    {
        string apiUrl = $"https://api.pirateweather.net/forecast/{pirateApiKey}/{latitude},{longitude}?units=si";

        using (HttpClient client = new HttpClient())
        {
            try
            {
                HttpResponseMessage response = await client.GetAsync(apiUrl);
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadAsStringAsync();
            }
            catch (HttpRequestException e)
            {
                DisplayError($"Error en la solicitud: {e.Message}");
                return null;
            }
        }
    }

    static async Task<(double? latitude, double? longitude)> GetCoordinatesAsync(string formattedName)
    {
        var locations = await GetLocationOptionsAsync(formattedName);
        var location = locations.FirstOrDefault(l => l.FormattedName == formattedName);

        if (location != null)
        {
            return (location.Latitude, location.Longitude);
        }

        return (null, null);
    }

    static void ParseAndDisplayWeather(string weatherData, string city)
    {
        try
        {
            JObject json = JObject.Parse(weatherData);

            string temperature = json["currently"]?["temperature"]?.Value<double>().ToString("F1") ?? "Desconocido";
            string summary = json["currently"]?["summary"]?.Value<string>() ?? "Desconocido";
            string translatedSummary = TranslateSummary(summary);
            string humidity = (json["currently"]?["humidity"]?.Value<double>() ?? 0).ToString("P0");
            string windSpeed = json["currently"]?["windSpeed"]?.Value<double>().ToString("F1") ?? "Desconocido";
            string uvIndex = json["currently"]?["uvIndex"]?.Value<int>().ToString() ?? "Desconocido";

            long sunriseTime = json["daily"]?["data"]?[0]?["sunriseTime"]?.Value<long>() ?? 0;
            long sunsetTime = json["daily"]?["data"]?[0]?["sunsetTime"]?.Value<long>() ?? 0;

            string sunrise = DateTimeOffset.FromUnixTimeSeconds(sunriseTime).LocalDateTime.ToString("HH:mm");
            string sunset = DateTimeOffset.FromUnixTimeSeconds(sunsetTime).LocalDateTime.ToString("HH:mm");

            var panel = new Panel($@"Temperatura: [bold blue]{temperature}°C[/]
Condición: [bold green]{translatedSummary}[/]
Humedad: [bold cyan]{humidity}%[/]
Velocidad del Viento: [bold yellow]{windSpeed} m/s[/]
Índice UV: [bold magenta]{uvIndex}[/]
Amanecer: [bold orange3]{sunrise}[/]
Atardecer: [bold orange3]{sunset}[/]")
            {
                Border = BoxBorder.Rounded,
                Expand = false,
                Header = new PanelHeader($"Pronóstico del Tiempo"),
                Padding = new Padding(2, 1, 2, 1),
            };

            AnsiConsole.Write(panel);
        }
        catch (Exception ex)
        {
            DisplayError($"Error al analizar los datos del clima: {ex.Message}");
        }
    }

    static void ParseAndDisplayForecast(string weatherData)
    {
        try
        {
            JObject json = JObject.Parse(weatherData);
            JArray dailyData = json["daily"]?["data"] as JArray;

            if (dailyData != null)
            {
                var table = new Table();
                table.AddColumn("Día");
                table.AddColumn("Temp. Máx. (°C)");
                table.AddColumn("Temp. Mín. (°C)");
                table.AddColumn("Resumen");
                table.AddColumn("Prob. Lluvia");

                foreach (var day in dailyData.Take(7))
                {
                    string daySummary = day["summary"]?.Value<string>() ?? "Desconocido";
                    string translatedSummary = TranslateSummary(daySummary);

                    string dayMaxTemp = day["temperatureMax"]?.Value<double>().ToString("F1") ?? "Desconocido";
                    string dayMinTemp = day["temperatureMin"]?.Value<double>().ToString("F1") ?? "Desconocido";
                    string precipProbability = (day["precipProbability"]?.Value<double>() ?? 0).ToString("P0");

                    table.AddRow(
                        DateTimeOffset.FromUnixTimeSeconds(day["time"]?.Value<long>() ?? 0).DateTime.ToString("dddd"),
                        dayMaxTemp,
                        dayMinTemp,
                        translatedSummary,
                        $"{precipProbability}%"
                    );
                }

                AnsiConsole.Write(new Panel(table)
                {
                    Border = BoxBorder.Rounded,
                    Header = new PanelHeader("Pronóstico para los Próximos 7 Días"),
                    Padding = new Padding(2, 1, 2, 1),
                });
            }
        }
        catch (Exception ex)
        {
            DisplayError($"Error al analizar el pronóstico de los próximos 7 días: {ex.Message}");
        }
    }

    static string TranslateSummary(string summary)
    {
        for (int i = 0; i < customSummaries.GetLength(0); i++)
        {
            if (customSummaries[i, 0].Equals(summary, StringComparison.OrdinalIgnoreCase))
            {
                return customSummaries[i, 1];
            }
        }
        return summary;
    }

    static void DisplayHeader()
    {
        AnsiConsole.Write(
            new FigletText("CLIMA")
                .LeftJustified()
                .Color(Color.Cyan1));
    }

    static void DisplayError(string message)
    {
        AnsiConsole.MarkupLine($"[bold red]Error:[/] {message}");
    }
}

class LocationOption
{
    public string FormattedName { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
}