using System.Net;
using System.Net.Http.Json;

public class RateLimitTests : IClassFixture<LowRateLimitFactory>
{
    private readonly LowRateLimitFactory _factory;

    public RateLimitTests(LowRateLimitFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Login_ExceedsPermitLimit_ReturnsTooManyRequests()
    {
        var client = _factory.CreateClient();
        var body = new { Email = "nobody@apexpay.test", Password = "Password123!" };

        var first = await client.PostAsync("api/account/login", JsonContent.Create(body));
        var second = await client.PostAsync("api/account/login", JsonContent.Create(body));
        var third = await client.PostAsync("api/account/login", JsonContent.Create(body));

        Assert.Equal(HttpStatusCode.Unauthorized, first.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, second.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, third.StatusCode);
    }
}
