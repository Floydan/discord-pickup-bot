using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace PickupBot.GitHub.Models
{
    public class GitHubRelease
    {
        [JsonPropertyName("author")]
        public GitHubAuthor Author { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("body")]
        public string Body { get; set; }

        [JsonPropertyName("published_at")] 
        public DateTime PublishedDate { get; set; }

        [JsonPropertyName("html_url")]
        public string Url { get; set; }
    }

    public class GitHubAuthor
    {
        [JsonPropertyName("login")]
        public string Name { get; set; }
        [JsonPropertyName("avatar_url")]
        public string AvatarUrl { get; set; }
    }
}
