using System.Collections.Generic;
using System.Threading.Tasks;
using PickupBot.Translation.Models;

namespace PickupBot.Translation.Services
{
    public interface ITranslationService
    {
        Task<List<TranslationResult>> Translate(string targetLanguage, params string[] texts);

        string GetTargetLanguage(string emote);
    
    }
}
