using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

public class ApexPayFactory : WebApplicationFactory<Program>
{
    private readonly int _permitLimit;

    public ApexPayFactory() : this(100000)
    {
    }

    protected ApexPayFactory(int permitLimit)
    {
        _permitLimit = permitLimit;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RateLimit:Auth:PermitLimit"] = _permitLimit.ToString(),
                ["RateLimit:Auth:WindowSeconds"] = "60"
            });
        });
    }
}

public class LowRateLimitFactory : ApexPayFactory
{
    public LowRateLimitFactory() : base(2)
    {
    }
}
