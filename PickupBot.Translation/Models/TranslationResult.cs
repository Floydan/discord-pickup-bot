using AutoMapper;

namespace PickupBot.Translation.Models
{
    [AutoMap(typeof(Google.Cloud.Translation.V2.TranslationResult), DisableCtorValidation = true)]
    public class TranslationResult
    {
        /// <summary>
        /// The original text (or HTML) that was translated.
        /// </summary>
        public string OriginalText { get; }

        /// <summary>
        /// The translated text.
        /// </summary>
        public string TranslatedText { get; }

        /// <summary>
        /// The source language code detected by the server. This may
        /// be null, typically due to the source language being supplied
        /// by the caller.
        /// </summary>
        public string DetectedSourceLanguage { get; }

        /// <summary>
        /// The source language code supplied by the caller, if any.
        /// </summary>
        public string SpecifiedSourceLanguage { get; }

        /// <summary>
        /// The target language code that the text was translated into.
        /// </summary>
        public string TargetLanguage { get; }

        /// <summary>
        /// Constructs an instance.
        /// </summary>
        /// <remarks>This constructor is for the benefit of testability, in case you wish to provide your own
        /// fake implementation of <see cref="TranslationClient"/>.</remarks>
        /// <param name="originalText">The original text.</param>
        /// <param name="translatedText">The translated text.</param>
        /// <param name="detectedSourceLanguage">The source language code detected by the server, if any.</param>
        /// <param name="specifiedSourceLanguage">The source language code specified by the API caller.</param>
        /// <param name="targetLanguage">The target language code.</param>
        public TranslationResult(string originalText, string translatedText, string detectedSourceLanguage, string specifiedSourceLanguage,
            string targetLanguage)
        {
            OriginalText = originalText;
            TranslatedText = translatedText;
            SpecifiedSourceLanguage = specifiedSourceLanguage;
            DetectedSourceLanguage = detectedSourceLanguage;
            TargetLanguage = targetLanguage;
        }

        public TranslationResult()
        {
            
        }
    }
}
