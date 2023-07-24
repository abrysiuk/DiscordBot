using System.Net.Http.Headers;
using System.Text.Json;


namespace GrammarCheck
{
    
    internal class Check
    {
        public static async Task<Response?> ProcessText(string text)
        {
            return await ProcessText(text, "https://api.languagetoolplus.com/v2/check");
        }
        public static async Task<Response?> ProcessText(string text, string api)
        {
            using HttpClient client = new();
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            client.DefaultRequestHeaders.Add("User-Agent", "DiscordDBot");

            var formContent = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("text", text),
                new KeyValuePair<string, string>("language", "en-ca")
            });

            var response = await client.PostAsync(api, formContent);

            if (response.IsSuccessStatusCode)
            {
                await using Stream stream = await response.Content.ReadAsStreamAsync();
                var corrections = await JsonSerializer.DeserializeAsync<Response>(stream);

                return corrections;
            }
            else
            {
                return null;
            }
        }
    }
    public class Response
    {
        public Software software { get; set; } = new Software();
        public Language language { get; set; } = new Language();
        public Match[] matches { get; set; } = Array.Empty<Match>();
    }
    public class Software
    {
        public string name { get; set; } = string.Empty;
        public string version { get; set; } = string.Empty;
        public string buildDate { get; set; } = string.Empty;
        public int apiVersion { get; set; }
        public string? status { get; set; }
        public bool? premium { get; set; }
    }
    public class Language
    {
        public string name { get; set; } = string.Empty;
        public string code { get; set; } = string.Empty;
        public Language? detectedLanguage { get; set; }
    }

    public class Match
    {
        public string message { get; set; } = string.Empty;
        public string shortMessage { get; set; } = string.Empty;
        public int offset { get; set; }
        public int length { get; set; }
        public Replacement[]? replacements { get; set; }
        public Contenxt context { get; set; } = new Contenxt();
        public string sentence { get; set; } = string.Empty;
        public Rule? rule { get; set; }
    }
    public class Contenxt
    {
        public string text { get; set; } = string.Empty;
        public int offset { get; set; }
        public int length { get; set; }
    }
    public class Replacement
    {
        public string? value { get; set; }
    }
    public class Rule
    {
        public string id { get; set; } = string.Empty;
        public string subId { get; set; } = string.Empty;
        public string description { get; set; } = string.Empty;
        public URL[]? urls { get; set; }
        public string? issueType { get; set; }
        public Category category { get; set; } = new Category();
    }
    public class URL
    {
        public string value { get; set; } = string.Empty;
    }

    public class Category
    {
        public string id { get; set; } = string.Empty;
        public string name { get; set; } = string.Empty;
    }

}

