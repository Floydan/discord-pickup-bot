using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Google.Cloud.Translation.V2;
using TranslationResult = PickupBot.Translation.Models.TranslationResult;

namespace PickupBot.Translation.Services
{
    public class GoogleTranslationService : ITranslationService
    {
        private readonly string _googleTranslateApiKey;
        public GoogleTranslationService()
        {
            _googleTranslateApiKey = Environment.GetEnvironmentVariable("GoogleTranslateAPIKey") ?? "";
        }

        public async Task<List<TranslationResult>> Translate(string targetLanguage, params string[] texts)
        {
            using var client = TranslationClient.CreateFromApiKey(_googleTranslateApiKey, TranslationModel.Base);
            client.Service.HttpClient.DefaultRequestHeaders.Add("referer", "127.0.0.1");

            try
            {
                var googleResults = await client.TranslateTextAsync(texts, targetLanguage);
                var results = googleResults.Select(t => 
                    new TranslationResult(t.OriginalText, t.TranslatedText, t.DetectedSourceLanguage, t.SpecifiedSourceLanguage, t.TargetLanguage))
                    .ToList();
                return results;
            }
            catch (Exception e)
            {
                Console.WriteLine(e);

                return null;
            }
        }
        
        public string GetTargetLanguage(string emote)
        {
            return emote switch
            {
                "🇸🇪" => "sv",
                "🇫🇷" => "fr",
                "🇩🇪" => "de",
                "🇳🇱" => "nl",
                "🇳🇴" => "no",
                "🇫🇮" => "fi",
                "🇩🇰" => "da",
                "🇵🇱" => "pl",
                "🇪🇸" => "es",
                "🇮🇹" => "it",
                "🇬🇷" => "el",
                "🇵🇹" => "pt",
                "🇷🇺" => "ru",
                "🇬🇧" => "en",
                "🇺🇸" => "en",
                _ => null
            };
        }
    }
}