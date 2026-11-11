namespace SpotTool.Web.Features.Lead;

public class ClaudFlareService(IHttpClientFactory _httpClientFactory)
{
    public async Task<Tuple<bool, string?>> TestIfHumanAsync(string secretKey, string turnstileToken, CancellationToken token)
    {
        using var httpClient = _httpClientFactory.CreateClient("ClaudFlare");

        var postData = new FormUrlEncodedContent(
            [new KeyValuePair<string, string>("secret", secretKey), new KeyValuePair<string, string>("response", turnstileToken)]
        );
        
        var respond = await httpClient.PostAsync("https://challenges.cloudflare.com/turnstile/v0/siteverify", postData, token);

        // 1. Sprawdź, czy serwer Cloudflare w ogóle działa (status 200)
        if (!respond.IsSuccessStatusCode)
        {
            // Błąd sieciowy komunikacji z Cloudflare - np. brak internetu w kontenerze
            return new Tuple<bool, string?>(false, "Błąd sieciowy API Cloudflare");
        }

        // 2. Pobierz surowy tekst JSON z odpowiedzi
        string jsonString = await respond.Content.ReadAsStringAsync(token);

        try 
        {
            // 3. Sparsuj JSON, aby odczytać klucz "success"
            using var document = System.Text.Json.JsonDocument.Parse(jsonString);
            bool isHuman = document.RootElement.GetProperty("success").GetBoolean();

            // 4. Zwróć faktyczny wynik weryfikacji bota (isHuman) oraz surową odpowiedź
            return new Tuple<bool, string?>(isHuman, jsonString);
        }
        catch (Exception)
        {
            // Obsługa na wypadek, gdyby JSON był uszkodzony
            return new Tuple<bool, string?>(false, "Błąd parsowania odpowiedzi");
        }
    }
}