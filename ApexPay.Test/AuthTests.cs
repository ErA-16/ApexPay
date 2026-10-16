using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using ApexPay.Database;
using ApexPay.Dto;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

public class AuthTests : IClassFixture<ApexPayFactory>, IAsyncLifetime
{
    private static readonly JsonSerializerOptions Json = JsonSerializerOptions.Web;

    private readonly ApexPayFactory _factory;
    private readonly HttpClient _client;
    private readonly List<Guid> _createdAccountIds = new();

    public AuthTests(ApexPayFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        if (_createdAccountIds.Count == 0)
        {
            return;
        }

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApexPayDbContext>();

        var wallets = await db.Wallets.AsNoTracking()
            .Where(w => _createdAccountIds.Contains(w.AccountId))
            .ToListAsync();
        db.Wallets.RemoveRange(wallets);

        var accounts = await db.Accounts.AsNoTracking()
            .Where(a => _createdAccountIds.Contains(a.Id))
            .ToListAsync();
        db.Accounts.RemoveRange(accounts);

        await db.SaveChangesAsync();
    }

    private static string NewEmail() => $"{Guid.NewGuid():N}@apexpay.test";

    private static HttpRequestMessage Auth(HttpMethod method, string url, string token, object? body = null)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        if (body != null)
        {
            request.Content = JsonContent.Create(body);
        }

        return request;
    }

    private Task<HttpResponseMessage> PostAsync(string url, object body) =>
        _client.PostAsync(url, JsonContent.Create(body));

    private async Task<(Guid AccountId, string Email)> SignUpAsync(string? email = null, string password = "Password123!")
    {
        email ??= NewEmail();

        var response = await PostAsync("api/account/signup",
            new { Email = email, FirstName = "Test", LastName = "User", Password = password });
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<CreateAccountResponse>(Json);
        _createdAccountIds.Add(body!.Id);

        return (body.Id, email);
    }

    private Task<HttpResponseMessage> LoginAsync(string email, string password) =>
        PostAsync("api/account/login", new { Email = email, Password = password });

    private async Task<string> LoginTokenAsync(string email, string password = "Password123!")
    {
        var response = await LoginAsync(email, password);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<LoginResponse>(Json);
        return body!.AccessToken;
    }

    [Fact]
    public async Task SignUp_TooShortPassword_ReturnsBadRequest()
    {
        var response = await PostAsync("api/account/signup",
            new { Email = NewEmail(), FirstName = "Test", LastName = "User", Password = "short" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task SignUp_TooLongPassword_ReturnsBadRequest()
    {
        var response = await PostAsync("api/account/signup",
            new { Email = NewEmail(), FirstName = "Test", LastName = "User", Password = new string('a', 101) });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task SignUp_MissingFirstName_ReturnsBadRequest()
    {
        var response = await PostAsync("api/account/signup",
            new { Email = NewEmail(), FirstName = "", LastName = "User", Password = "Password123!" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task SignUp_DuplicateEmail_CaseInsensitive_ReturnsConflict()
    {
        var (_, email) = await SignUpAsync();

        var second = await PostAsync("api/account/signup",
            new { Email = email.ToUpperInvariant(), FirstName = "Test", LastName = "User", Password = "Password123!" });

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task Login_ValidCredentials_ReturnsToken()
    {
        var (_, email) = await SignUpAsync();

        var response = await LoginAsync(email, "Password123!");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<LoginResponse>(Json);
        Assert.NotNull(body);
        Assert.False(string.IsNullOrWhiteSpace(body.AccessToken));
        Assert.True(body.ExpiresAt > DateTime.UtcNow);
    }

    [Fact]
    public async Task Login_WrongPassword_ReturnsUnauthorizedWithGenericMessage()
    {
        var (_, email) = await SignUpAsync();

        var response = await LoginAsync(email, "WrongPassword123!");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("Invalid email or password", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Login_UnknownEmail_ReturnsUnauthorizedWithGenericMessage()
    {
        var response = await LoginAsync(NewEmail(), "Password123!");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("Invalid email or password", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Login_MerchantAccountWithoutPassword_ReturnsUnauthorized()
    {
        var response = await LoginAsync("merchant@apexpay.test", "Password123!");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Wallets_WithoutToken_ReturnsUnauthorized()
    {
        var response = await _client.GetAsync("api/wallet/wallets");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task WalletHistory_NotOwned_ReturnsNotFound()
    {
        var (_, ownerEmail) = await SignUpAsync();
        var ownerToken = await LoginTokenAsync(ownerEmail);

        var create = await _client.SendAsync(Auth(HttpMethod.Post, "api/wallet/create-wallet", ownerToken, new { Name = "Personal" }));
        create.EnsureSuccessStatusCode();
        var wallet = await create.Content.ReadFromJsonAsync<WalletResponse>(Json);

        var (_, otherEmail) = await SignUpAsync();
        var otherToken = await LoginTokenAsync(otherEmail);

        var history = await _client.SendAsync(Auth(HttpMethod.Get, $"api/wallet/{wallet!.Id}/history", otherToken));
        Assert.Equal(HttpStatusCode.NotFound, history.StatusCode);

        var byId = await _client.SendAsync(Auth(HttpMethod.Get, $"api/wallet/{wallet.Id}", otherToken));
        Assert.Equal(HttpStatusCode.NotFound, byId.StatusCode);
    }
}
