#nullable enable

using System;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using PixelCatsClient;
using Xunit;

namespace ConsoleTest.Tests
{
    // Very small fake handler for deterministic responses
    internal sealed class FakeHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;
        public FakeHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
            => _responder = responder ?? throw new ArgumentNullException(nameof(responder));

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(_responder(request));
    }

    public sealed class PixelCatsApiClientTests
    {
        [Fact]
        public async Task SubmitCodeAsync_ReturnsTrue_On200()
        {
            var handler = new FakeHttpMessageHandler(req =>
            {
                Assert.Equal(HttpMethod.Post, req.Method);

                // Avoid nullable warning by asserting non-null before using it
                Assert.NotNull(req.RequestUri);
                Assert.Equal("/api/codes", req.RequestUri!.PathAndQuery);

                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{\"ok\":true}")
                };
            });

            using var http = new HttpClient(handler) { BaseAddress = new Uri("http://api.test") };
            var client = new PixelCatsApiClient(http);

            var ok = await client.SubmitCodeAsync("ABC123", 9001, "Tetris", "key");
            Assert.True(ok);
        }

        [Fact]
        public async Task SubmitCodeAsync_ReturnsFalse_OnServerError()
        {
            var handler = new FakeHttpMessageHandler(_ =>
                new HttpResponseMessage(HttpStatusCode.InternalServerError));

            using var http = new HttpClient(handler) { BaseAddress = new Uri("http://api.test") };
            var client = new PixelCatsApiClient(http);

            // Fixes CS8625: SubmitCodeAsync now requires non-null gameName, so pass a real value.
            var ok = await client.SubmitCodeAsync("X", 1, "Tetris");
            Assert.False(ok);
        }

        [Fact]
        public async Task GetTopScoresAsync_ParsesJson_On200()
        {
            var payload = new[]
            {
                new { id = 1, name = "Alice", score = 100, created_at = "2026-01-01T00:00:00Z" },
                new { id = 2, name = "Bob", score = 80, created_at = "2026-01-02T00:00:00Z" }
            };
            var json = JsonSerializer.Serialize(payload);

            var handler = new FakeHttpMessageHandler(req =>
            {
                Assert.Equal(HttpMethod.Get, req.Method);
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
                };
            });

            using var http = new HttpClient(handler) { BaseAddress = new Uri("http://api.test") };
            var client = new PixelCatsApiClient(http);

            // Client now guarantees non-null array
            var results = await client.GetTopScoresAsync(2);

            Assert.Equal(2, results.Length);
            Assert.Equal("Alice", results[0].name);
            Assert.Equal(100, results[0].score);
        }
    }
}
