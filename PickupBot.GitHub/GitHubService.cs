using System;
using System.Collections.Generic;
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

        public async Task<string> GetCommits(int count = 10)
        {
            var response = await _client.GetAsync("/repos/Floydan/discord-pickup-bot/commits");
            
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadAsStringAsync();
            }

            return $"StatusCode: {response.StatusCode}";
        }

        public async Task<IEnumerable<GitHubRelease>> GetReleases(int count = 3)
        {
            var response = await _client.GetAsync("/repos/Floydan/discord-pickup-bot/releases");

            if (!response.IsSuccessStatusCode) return null;

            await using var stream = await response.Content.ReadAsStreamAsync();
            return await JsonSerializer.DeserializeAsync<IEnumerable<GitHubRelease>>(stream);

        }
    }
}
