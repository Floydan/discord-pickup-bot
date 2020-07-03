using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using Moq.Protected;
using NUnit.Framework;
using PickupBot.GitHub.Models;

namespace PickupBot.GitHub.Test
{
    public class GitHubServiceTests
    {
        private Mock<HttpMessageHandler> _releasesHandlerMock;
        private const string ReleasesJson = @"[
  {
    ""url"": ""https://api.github.com/repos/octocat/Hello-World/commits/6dcb09b5b57875f334f61aebed695e2e4193db5e"",
    ""sha"": ""6dcb09b5b57875f334f61aebed695e2e4193db5e"",
    ""name"": ""v9.9.9"",
    ""node_id"": ""MDY6Q29tbWl0NmRjYjA5YjViNTc4NzVmMzM0ZjYxYWViZWQ2OTVlMmU0MTkzZGI1ZQ=="",
    ""html_url"": ""https://github.com/octocat/Hello-World/commit/6dcb09b5b57875f334f61aebed695e2e4193db5e"",
    ""comments_url"": ""https://api.github.com/repos/octocat/Hello-World/commits/6dcb09b5b57875f334f61aebed695e2e4193db5e/comments"",
    ""body"": ""## Commits\n- [[f27a8dc](https://github.com/Floydan/discord-pickup-bot/commit/f27a8dc59de2c7c2c0b246c01bd04da79858e903)]: Added Group to server module, cleaned up in public module (Floydan)"",
    ""commit"": {
      ""url"": ""https://api.github.com/repos/octocat/Hello-World/git/commits/6dcb09b5b57875f334f61aebed695e2e4193db5e"",
      ""author"": {
        ""name"": ""Monalisa Octocat"",
        ""email"": ""support@github.com"",
        ""date"": ""2011-04-14T16:00:49Z""
      },
      ""committer"": {
        ""name"": ""Monalisa Octocat"",
        ""email"": ""support@github.com"",
        ""date"": ""2011-04-14T16:00:49Z""
      },
      ""message"": ""Fix all the bugs"",
      ""tree"": {
        ""url"": ""https://api.github.com/repos/octocat/Hello-World/tree/6dcb09b5b57875f334f61aebed695e2e4193db5e"",
        ""sha"": ""6dcb09b5b57875f334f61aebed695e2e4193db5e""
      },
      ""comment_count"": 0,
      ""verification"": {
        ""verified"": false,
        ""reason"": ""unsigned"",
        ""signature"": null,
        ""payload"": null
      }
    },
    ""author"": {
      ""login"": ""octocat"",
      ""id"": 1,
      ""node_id"": ""MDQ6VXNlcjE="",
      ""avatar_url"": ""https://github.com/images/error/octocat_happy.gif"",
      ""gravatar_id"": """",
      ""url"": ""https://api.github.com/users/octocat"",
      ""html_url"": ""https://github.com/octocat"",
      ""followers_url"": ""https://api.github.com/users/octocat/followers"",
      ""following_url"": ""https://api.github.com/users/octocat/following{/other_user}"",
      ""gists_url"": ""https://api.github.com/users/octocat/gists{/gist_id}"",
      ""starred_url"": ""https://api.github.com/users/octocat/starred{/owner}{/repo}"",
      ""subscriptions_url"": ""https://api.github.com/users/octocat/subscriptions"",
      ""organizations_url"": ""https://api.github.com/users/octocat/orgs"",
      ""repos_url"": ""https://api.github.com/users/octocat/repos"",
      ""events_url"": ""https://api.github.com/users/octocat/events{/privacy}"",
      ""received_events_url"": ""https://api.github.com/users/octocat/received_events"",
      ""type"": ""User"",
      ""site_admin"": false
    },
    ""committer"": {
      ""login"": ""octocat"",
      ""id"": 1,
      ""node_id"": ""MDQ6VXNlcjE="",
      ""avatar_url"": ""https://github.com/images/error/octocat_happy.gif"",
      ""gravatar_id"": """",
      ""url"": ""https://api.github.com/users/octocat"",
      ""html_url"": ""https://github.com/octocat"",
      ""followers_url"": ""https://api.github.com/users/octocat/followers"",
      ""following_url"": ""https://api.github.com/users/octocat/following{/other_user}"",
      ""gists_url"": ""https://api.github.com/users/octocat/gists{/gist_id}"",
      ""starred_url"": ""https://api.github.com/users/octocat/starred{/owner}{/repo}"",
      ""subscriptions_url"": ""https://api.github.com/users/octocat/subscriptions"",
      ""organizations_url"": ""https://api.github.com/users/octocat/orgs"",
      ""repos_url"": ""https://api.github.com/users/octocat/repos"",
      ""events_url"": ""https://api.github.com/users/octocat/events{/privacy}"",
      ""received_events_url"": ""https://api.github.com/users/octocat/received_events"",
      ""type"": ""User"",
      ""site_admin"": false
    },
    ""parents"": [
      {
        ""url"": ""https://api.github.com/repos/octocat/Hello-World/commits/6dcb09b5b57875f334f61aebed695e2e4193db5e"",
        ""sha"": ""6dcb09b5b57875f334f61aebed695e2e4193db5e""
      }
    ]
  }
]";

        [SetUp]
        public void Setup()
        {
            _releasesHandlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);
            _releasesHandlerMock
                .Protected()
                // Setup the PROTECTED method to mock
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>()
                )
                // prepare the expected response of the mocked http call
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent(ReleasesJson)
                })
                .Verifiable();
        }

        [Test]
        public async Task TestReleases()
        {
            var httpClient = new HttpClient(_releasesHandlerMock.Object)
            {
                BaseAddress = new Uri("https://api.github.com/")
            };
            
            httpClient.DefaultRequestHeaders.Add("Accept", "application/vnd.github.v3+json");
            httpClient.DefaultRequestHeaders.Add("User-Agent", "Discord-Pickup-Bot");

            var githubService = new GitHubService(httpClient);

            Assert.NotNull(githubService);

            var releases = await githubService.GetReleases(1);
            
            Assert.NotNull(releases);

            var gitHubReleases = releases as GitHubRelease[] ?? releases.ToArray();
            Assert.AreEqual(1, gitHubReleases.Count());

            var first = gitHubReleases.First();
            Assert.IsNotNull(first.Name);
            Assert.IsNotEmpty(first.Name);

            var expectedUri = new Uri("https://api.github.com/repos/Floydan/discord-pickup-bot/releases");
            _releasesHandlerMock.Protected().Verify(
                "SendAsync",
                Times.Exactly(1), // we expected a single external request
                ItExpr.Is<HttpRequestMessage>(req =>
                        req.Method == HttpMethod.Get  // we expected a GET request
                        && req.RequestUri == expectedUri // to this uri
                ),
                ItExpr.IsAny<CancellationToken>()
            );
        }
    }
}