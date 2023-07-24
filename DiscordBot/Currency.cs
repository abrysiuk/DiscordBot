using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;
using System.Text.Json.Nodes;
namespace DiscordBot
{
    /// <summary>
    /// Object to represent a currency and download them to the database.
    /// </summary>
    public class Currency
    {
        [Key, DatabaseGenerated(DatabaseGeneratedOption.None)]
        public string CurrencyCode { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public float Rate { get; set; }
        public DateTime Updated { get; set; } = DateTime.Now;
        public int LCID { get; set; }
        public Currency() { }
        public float ToCurrency(Currency toCurrency, float amount = 1)
        {
            return BetweenCurrencies(this, toCurrency, amount);
        }
        public float FromCurrency(Currency fromCurrency, float amount = 1)
        {
            return BetweenCurrencies(fromCurrency,this,amount);
        }
        public static float BetweenCurrencies(Currency fromCurrency, Currency toCurrency, float amount)
        {
            return (amount / fromCurrency.Rate) * toCurrency.Rate;
        }
        public async static Task DownloadCurrency(AppDBContext db)
        {
            var currencies = db.Currencies;

            if (currencies.Any(x => x.Updated.Date < DateTime.Today) || !currencies.Any())
            {
                var client = new HttpClient();
                HttpRequestMessage request;

                request = new HttpRequestMessage
                {
                    Method = HttpMethod.Get,
                    RequestUri = new Uri("https://currency-converter-pro1.p.rapidapi.com/latest-rates?base=USD"),
                    Headers =
                                                {
                                                    { "X-RapidAPI-Key", Program.Configuration["RapidAPIKey"] },
                                                    { "X-RapidAPI-Host", "currency-converter-pro1.p.rapidapi.com" },
                                                },
                };
                using var httpResponse = await client.SendAsync(request);
                if (httpResponse.IsSuccessStatusCode && httpResponse.Content is not null && httpResponse.Content.Headers.ContentType?.MediaType == "application/json")
                {
                    var contentStream = await httpResponse.Content.ReadAsStreamAsync();

                    try
                    {
                        var result = JsonNode.Parse(contentStream);
                        var results = result?["result"]?.Deserialize<Dictionary<string, float>>();

                        if (results == null) { return; }

                        foreach (var rate in results)
                        {
                            var existing = currencies.Find(rate.Key);
                            if (existing != null)
                            {
                                existing.Rate = rate.Value;
                                existing.Updated = DateTime.Now;
                            }
                            else
                            {
                                currencies.Add(new Currency() { CurrencyCode = rate.Key, Rate = rate.Value, Updated = DateTime.Now });
                            }
                        }
                    }
                    catch (JsonException) // Invalid JSON
                    {
                        await Program.Log(Discord.LogSeverity.Error, "Currency", "Invalid JSON.");
                    }
                }
                else
                {
                    await Program.Log(Discord.LogSeverity.Error, "Currency", "HTTP Response was invalid and cannot be deserialised.");
                }
            }
            db.SaveChanges();
        }
    }
}
