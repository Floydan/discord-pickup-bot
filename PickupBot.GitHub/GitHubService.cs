using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using PickupBot.GitHub.Models;

namespace PickupBot.GitHub
{
    public class GitHubService
    {
        private readonly HttpClient _client;

        public GitHubService(HttpClient client)
        {
            client.BaseAddress = new Uri("https://api.github.com/");
            
            client.DefaultRequestHeaders.Add("Accept", "application/vnd.github.v3+json");
            client.DefaultRequestHeaders.Add("User-Agent", "Discord-Pickup-Bot");

            _client = client;
        }

        public async Task<IEnumerable<GitHubRelease>> GetReleases(int count = 3)
        {
            var response = await _client.GetAsync("/repos/Floydan/discord-pickup-bot/releases");

            if (!response.IsSuccessStatusCode) return null;

            await using var stream = await response.Content.ReadAsStreamAsync();
            return (await JsonSerializer.DeserializeAsync<IEnumerable<GitHubRelease>>(stream)).Take(count);

        }
    }
}
