using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using Google.Cloud.Translation.V2;
using Microsoft.Extensions.Logging;
using PickupBot.Data.Models;
using TranslationResult = PickupBot.Translation.Models.TranslationResult;

namespace PickupBot.Translation.Services
{
    public class GoogleTranslationService : ITranslationService
    {
        private readonly IMapper _mapper;
        private readonly ILogger<GoogleTranslationService> _logger;
        private readonly string _googleTranslateApiKey;
        public GoogleTranslationService(PickupBotSettings pickupBotSettings, IMapper mapper, ILogger<GoogleTranslationService> logger)
        {
            _mapper = mapper;
            _logger = logger;
            _googleTranslateApiKey = pickupBotSettings.GoogleTranslateAPIKey ?? "";
        }

        public async Task<List<TranslationResult>> Translate(string targetLanguage, params string[] texts)
        {
            using var client = TranslationClient.CreateFromApiKey(_googleTranslateApiKey, TranslationModel.Base);
            client.Service.HttpClient.DefaultRequestHeaders.Add("referer", "127.0.0.1");

            try
            {
                var googleResults = await client.TranslateTextAsync(texts, targetLanguage);
                var results = googleResults.Select(_mapper.Map<TranslationResult>).ToList();
                return results;
            }
            catch (Exception e)
            {
                _logger.LogError(e.Message, e);
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