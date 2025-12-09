using System.Net.Http;
using System.Threading.Tasks;
using Xunit;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.VisualStudio.TestPlatform.TestHost;

namespace xbytechat.api.Tests
{
    public class ProductApiTests : IClassFixture<WebApplicationFactory<Program>>
    {
        private readonly HttpClient _client;

        public ProductApiTests(WebApplicationFactory<Program> factory)
        {
            _client = factory.CreateClient();
        }

        [Fact]
        public async Task GetAllProducts_ShouldReturnSuccess()
        {
            var response = await _client.GetAsync("/api/products");
            response.EnsureSuccessStatusCode();


        }
 
    }
}
