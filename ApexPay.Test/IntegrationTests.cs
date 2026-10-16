using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using ApexPay.Database;
using ApexPay.Dto;
using ApexPay.Models;
using ApexPay.Service;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

public class IntegrationTests : IClassFixture<ApexPayFactory>, IAsyncLifetime
{
    private static readonly JsonSerializerOptions Json = JsonSerializerOptions.Web;

    private readonly ApexPayFactory _factory;
    private readonly HttpClient _client;
    private readonly List<Guid> _createdAccountIds = new();
    private readonly List<Guid> _createdTransactionIds = new();
    private readonly List<string> _usedIdempotencyKeys = new();

    private Guid _merchantWalletId;

    public IntegrationTests(ApexPayFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    public async Task InitializeAsync()
    {
        _merchantWalletId = await CreateMerchantWalletAsync();
    }

    private async Task<Guid> CreateMerchantWalletAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApexPayDbContext>();

        var account = new Account
        {
            FullName = "Test Merchant",
            Email = $"merchant-{Guid.NewGuid():N}@apexpay.test"
        };

        db.Accounts.Add(account);
        await db.SaveChangesAsync();
        _createdAccountIds.Add(account.Id);

        for (int attempt = 1; attempt <= 5; attempt++)
        {
            var wallet = new Wallet
            {
                AccountId = account.Id,
                AccountNumber = WalletService.GenerateAccountNumber(),
                Name = "Merchant Settlement",
                Role = WalletRole.Merchant
            };

            db.Wallets.Add(wallet);

            try
            {
                await db.SaveChangesAsync();
                return wallet.Id;
            }
            catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
            {
                db.ChangeTracker.Clear();
            }
        }

        throw new InvalidOperationException("Could not create a merchant wallet for the test.");
    }

    public async Task DisposeAsync()
    {
        if (_createdAccountIds.Count == 0 && _createdTransactionIds.Count == 0 && _usedIdempotencyKeys.Count == 0)
        {
            return;
        }

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApexPayDbContext>();

        var walletIds = await db.Wallets.AsNoTracking()
            .Where(w => _createdAccountIds.Contains(w.AccountId))
            .Select(w => w.Id)
            .ToListAsync();

        var ledgerEntries = await db.LedgerEntries.AsNoTracking()
            .Where(e => walletIds.Contains(e.WalletId) || _createdTransactionIds.Contains(e.TransactionId))
            .ToListAsync();
        db.LedgerEntries.RemoveRange(ledgerEntries);
        await db.SaveChangesAsync();

        var transactions = await db.Transactions.AsNoTracking()
            .Where(t => _createdTransactionIds.Contains(t.Id)
                     || (t.SourceWalletId != null && walletIds.Contains(t.SourceWalletId.Value))
                     || walletIds.Contains(t.DestinationWalletId))
            .ToListAsync();
        db.Transactions.RemoveRange(transactions);
        await db.SaveChangesAsync();

        var resetTokens = await db.PasswordResetTokens.AsNoTracking()
            .Where(t => _createdAccountIds.Contains(t.AccountId))
            .ToListAsync();
        db.PasswordResetTokens.RemoveRange(resetTokens);

        var wallets = await db.Wallets.AsNoTracking()
            .Where(w => _createdAccountIds.Contains(w.AccountId))
            .ToListAsync();
        db.Wallets.RemoveRange(wallets);
        await db.SaveChangesAsync();

        var accounts = await db.Accounts.AsNoTracking()
            .Where(a => _createdAccountIds.Contains(a.Id))
            .ToListAsync();
        db.Accounts.RemoveRange(accounts);

        var idempotencyRecords = await db.Idempotencies.AsNoTracking()
            .Where(i => _usedIdempotencyKeys.Contains(i.Key))
            .ToListAsync();
        db.Idempotencies.RemoveRange(idempotencyRecords);

        await db.SaveChangesAsync();
    }

    private async Task<HttpResponseMessage> PostJsonAsync(string url, object body, Guid? idempotencyKey = null, string? token = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(body)
        };

        if (token != null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        if (idempotencyKey.HasValue)
        {
            _usedIdempotencyKeys.Add(idempotencyKey.Value.ToString());
            request.Headers.Add("Idempotency-Key", idempotencyKey.Value.ToString());
        }

        return await _client.SendAsync(request);
    }

    private Task<HttpResponseMessage> GetAsync(string url, string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return _client.SendAsync(request);
    }

    private Task<HttpResponseMessage> PatchAsync(string url, string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Patch, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return _client.SendAsync(request);
    }

    private async Task<T> QueryDbAsync<T>(Func<ApexPayDbContext, Task<T>> query)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApexPayDbContext>();
        return await query(db);
    }

    private Task<Idempotency?> GetIdempotencyAsync(Guid key) =>
        QueryDbAsync(db => db.Idempotencies.AsNoTracking()
            .FirstOrDefaultAsync(i => i.Key == key.ToString()));

    private async Task<(Guid AccountId, string Token)> SignUpAndLoginAsync(string? email = null, string password = "Password123!")
    {
        email ??= $"{Guid.NewGuid():N}@apexpay.test";

        var signUp = await PostJsonAsync("api/account/signup",
            new { Email = email, FirstName = "Test", LastName = "User", Password = password });
        signUp.EnsureSuccessStatusCode();
        var account = await signUp.Content.ReadFromJsonAsync<CreateAccountResponse>(Json);
        _createdAccountIds.Add(account!.Id);

        var login = await PostJsonAsync("api/account/login", new { Email = email, Password = password });
        login.EnsureSuccessStatusCode();
        var loginBody = await login.Content.ReadFromJsonAsync<LoginResponse>(Json);

        return (account.Id, loginBody!.AccessToken);
    }

    private async Task<WalletResponse> CreateWalletAsync(string token, string name)
    {
        var response = await PostJsonAsync("api/wallet/create-wallet", new { Name = name }, token: token);
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<WalletResponse>(Json))!;
    }

    private async Task<FundWalletResponse> FundAsync(Guid walletId, decimal amount, string token)
    {
        var key = Guid.NewGuid();
        var response = await PostJsonAsync("api/wallet/fund", new { WalletId = walletId, Amount = amount }, key, token);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<FundWalletResponse>(Json);
        _createdTransactionIds.Add(body!.TransactionId);
        return body;
    }

    private Task<decimal> GetBalanceAsync(Guid walletId) =>
        QueryDbAsync(db => db.Wallets.AsNoTracking()
            .Where(w => w.Id == walletId)
            .Select(w => w.Balance)
            .SingleAsync());

    [Fact]
    public async Task SignUp_ValidEmail_ReturnsCreated()
    {
        var email = $"{Guid.NewGuid():N}@apexpay.test";

        var response = await PostJsonAsync("api/account/signup",
            new { Email = email, FirstName = "John", LastName = "Doe", Password = "Password123!" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<CreateAccountResponse>(Json);
        Assert.NotNull(body);
        Assert.NotEqual(Guid.Empty, body.Id);
        Assert.Equal("John Doe", body.FullName);
        Assert.Equal(email, body.Email);
        _createdAccountIds.Add(body.Id);
    }

    [Fact]
    public async Task SignUp_DuplicateEmail_ReturnsConflict()
    {
        var email = $"{Guid.NewGuid():N}@apexpay.test";
        var first = await PostJsonAsync("api/account/signup",
            new { Email = email, FirstName = "John", LastName = "Doe", Password = "Password123!" });
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        var body = await first.Content.ReadFromJsonAsync<CreateAccountResponse>(Json);
        _createdAccountIds.Add(body!.Id);

        var second = await PostJsonAsync("api/account/signup",
            new { Email = email, FirstName = "John", LastName = "Doe", Password = "Password123!" });

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task CreateWallet_Valid_ReturnsCreated()
    {
        var (_, token) = await SignUpAndLoginAsync();

        var response = await PostJsonAsync("api/wallet/create-wallet", new { Name = "Personal" }, token: token);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<WalletResponse>(Json);
        Assert.NotNull(body);
        Assert.NotEqual(Guid.Empty, body.Id);
        Assert.Equal("Personal", body.Name);
        Assert.Equal(0m, body.Balance);
        Assert.Equal("ACTIVE", body.Status);
        Assert.Equal(10, body.AccountNumber.Length);
    }

    [Fact]
    public async Task CreateWallet_DuplicateName_ReturnsConflict()
    {
        var (_, token) = await SignUpAndLoginAsync();
        var first = await CreateWalletAsync(token, "Personal");
        Assert.NotEqual(Guid.Empty, first.Id);

        var second = await PostJsonAsync("api/wallet/create-wallet", new { Name = "Personal" }, token: token);

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task GetAllWallets_NoWallets_ReturnsOkEmptyList()
    {
        var (userId, token) = await SignUpAndLoginAsync();

        var response = await GetAsync("api/wallet/wallets", token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<WalletListResponse>(Json);
        Assert.NotNull(body);
        Assert.Equal(userId, body.UserId);
        Assert.Equal(1, body.Page);
        Assert.Equal(20, body.Limit);
        Assert.Equal(0, body.TotalWallets);
        Assert.Equal(0, body.TotalPages);
        Assert.Empty(body.Wallets);
    }

    [Fact]
    public async Task GetAllWallets_PaginatesInStableOrder()
    {
        var (userId, token) = await SignUpAndLoginAsync();
        await CreateWalletAsync(token, "W1");
        await CreateWalletAsync(token, "W2");
        await CreateWalletAsync(token, "W3");

        var expectedIds = await QueryDbAsync(db => db.Wallets.AsNoTracking()
            .Where(w => w.AccountId == userId && w.Status != WalletStatus.Closed)
            .OrderBy(w => w.CreatedAt).ThenBy(w => w.Id)
            .Select(w => w.Id)
            .ToListAsync());
        Assert.Equal(3, expectedIds.Count);

        var firstPage = await GetAsync("api/wallet/wallets?page=1&limit=2", token);
        Assert.Equal(HttpStatusCode.OK, firstPage.StatusCode);
        var firstBody = await firstPage.Content.ReadFromJsonAsync<WalletListResponse>(Json);
        Assert.NotNull(firstBody);
        Assert.Equal(3, firstBody.TotalWallets);
        Assert.Equal(2, firstBody.TotalPages);
        Assert.Equal(expectedIds.Take(2), firstBody.Wallets.Select(w => w.Id));

        var secondPage = await GetAsync("api/wallet/wallets?page=2&limit=2", token);
        Assert.Equal(HttpStatusCode.OK, secondPage.StatusCode);
        var secondBody = await secondPage.Content.ReadFromJsonAsync<WalletListResponse>(Json);
        Assert.NotNull(secondBody);
        Assert.Equal(expectedIds.Skip(2), secondBody.Wallets.Select(w => w.Id));
    }

    [Theory]
    [InlineData("page=0")]
    [InlineData("page=-1")]
    [InlineData("limit=0")]
    [InlineData("limit=101")]
    public async Task GetAllWallets_InvalidPaging_ReturnsBadRequest(string query)
    {
        var (_, token) = await SignUpAndLoginAsync();

        var response = await GetAsync($"api/wallet/wallets?{query}", token);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetWalletHistory_FundAndTransfer_ReturnsConsistentLedger()
    {
        var (_, token) = await SignUpAndLoginAsync();
        var source = await CreateWalletAsync(token, "Main");
        var destination = await CreateWalletAsync(token, "Spending");

        await FundAsync(source.Id, 200m, token);

        var transferResponse = await PostJsonAsync($"api/wallet/{source.Id}/transfer",
            new { DestinationWalletId = destination.Id, Amount = 100m }, Guid.NewGuid(), token);
        transferResponse.EnsureSuccessStatusCode();
        var transfer = await transferResponse.Content.ReadFromJsonAsync<WalletTransferResponse>(Json);
        _createdTransactionIds.Add(transfer!.TransactionId);

        var response = await GetAsync($"api/wallet/{source.Id}/history", token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<WalletHistoryResponse>(Json);
        Assert.NotNull(body);
        Assert.Equal(source.Id, body.WalletId);
        Assert.Equal(1, body.Page);
        Assert.Equal(20, body.Limit);
        Assert.Equal(2, body.TotalEntries);
        Assert.Equal(1, body.TotalPages);
        Assert.Equal(100m, body.WalletStoredBalance);
        Assert.Equal(100m, body.LedgerCalculatedBalance);
        Assert.True(body.IsConsistent);
        Assert.Equal(2, body.Entries.Count);
        Assert.True(body.Entries[0].Timestamp >= body.Entries[1].Timestamp);

        var debit = Assert.Single(body.Entries, e => e.EntryType == "DEBIT");
        Assert.Equal(100m, debit.Amount);
        Assert.Equal("TRANSFER", debit.Type);
        Assert.False(string.IsNullOrWhiteSpace(debit.Reference));

        var credit = Assert.Single(body.Entries, e => e.EntryType == "CREDIT");
        Assert.Equal(200m, credit.Amount);
        Assert.Equal("FUNDING", credit.Type);
    }

    [Fact]
    public async Task GetWalletHistory_WalletNotOwned_ReturnsNotFound()
    {
        var (_, token) = await SignUpAndLoginAsync();

        var response = await GetAsync($"api/wallet/{Guid.NewGuid()}/history", token);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task FundWallet_ValidIdempotencyKey_ReturnsOk()
    {
        var (_, token) = await SignUpAndLoginAsync();
        var wallet = await CreateWalletAsync(token, "Funding");
        var key = Guid.NewGuid();

        var response = await PostJsonAsync("api/wallet/fund",
            new { WalletId = wallet.Id, Amount = 150.50m }, key, token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<FundWalletResponse>(Json);
        Assert.NotNull(body);
        Assert.Equal(wallet.Id, body.WalletId);
        Assert.Equal(150.50m, body.Amount);
        Assert.Equal(150.50m, body.NewBalance);
        Assert.Equal("Successful", body.State);
        Assert.False(string.IsNullOrWhiteSpace(body.Reference));
        _createdTransactionIds.Add(body.TransactionId);

        var balance = await GetBalanceAsync(wallet.Id);
        Assert.Equal(150.50m, balance);

        var ledgerRows = await QueryDbAsync(db => db.LedgerEntries.AsNoTracking()
            .Where(e => e.WalletId == wallet.Id && e.TransactionId == body.TransactionId)
            .ToListAsync());
        Assert.Single(ledgerRows);
        Assert.Equal(LedgerEntryType.Credit, ledgerRows[0].EntryType);
        Assert.Equal(150.50m, ledgerRows[0].Amount);
    }

    [Fact]
    public async Task FundWallet_AmountWithMoreThan2Decimals_ReturnsBadRequest()
    {
        var (_, token) = await SignUpAndLoginAsync();
        var wallet = await CreateWalletAsync(token, "Funding");
        var key = Guid.NewGuid();

        var response = await PostJsonAsync("api/wallet/fund",
            new { WalletId = wallet.Id, Amount = 100.123m }, key, token);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var stored = await QueryDbAsync(db => db.Idempotencies.AsNoTracking()
            .FirstOrDefaultAsync(i => i.Key == key.ToString()));
        Assert.Null(stored);

        var balance = await GetBalanceAsync(wallet.Id);
        Assert.Equal(0m, balance);
    }

    [Fact]
    public async Task FundWallet_SameIdempotencyKey_ReturnsStoredResponse()
    {
        var (_, token) = await SignUpAndLoginAsync();
        var wallet = await CreateWalletAsync(token, "Funding");
        var key = Guid.NewGuid();

        var first = await PostJsonAsync("api/wallet/fund",
            new { WalletId = wallet.Id, Amount = 100m }, key, token);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var firstBody = await first.Content.ReadFromJsonAsync<FundWalletResponse>(Json);
        _createdTransactionIds.Add(firstBody!.TransactionId);

        var second = await PostJsonAsync("api/wallet/fund",
            new { WalletId = wallet.Id, Amount = 100m }, key, token);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        var secondBody = await second.Content.ReadFromJsonAsync<FundWalletResponse>(Json);

        Assert.Equal(firstBody.Reference, secondBody!.Reference);
        Assert.Equal(firstBody.TransactionId, secondBody.TransactionId);
        Assert.Equal(firstBody.WalletId, secondBody.WalletId);
        Assert.Equal(firstBody.AccountNumber, secondBody.AccountNumber);
        Assert.Equal(firstBody.AccountName, secondBody.AccountName);
        Assert.Equal(firstBody.Amount, secondBody.Amount);
        Assert.Equal(firstBody.State, secondBody.State);
        Assert.Equal(firstBody.Narration, secondBody.Narration);
        Assert.Equal(100m, secondBody.NewBalance);

        var balance = await GetBalanceAsync(wallet.Id);
        Assert.Equal(100m, balance);

        var transactionCount = await QueryDbAsync(db => db.Transactions.AsNoTracking()
            .CountAsync(t => t.DestinationWalletId == wallet.Id));
        Assert.Equal(1, transactionCount);

        var ledgerCount = await QueryDbAsync(db => db.LedgerEntries.AsNoTracking()
            .CountAsync(e => e.WalletId == wallet.Id));
        Assert.Equal(1, ledgerCount);
    }

    [Fact]
    public async Task FundWallet_WalletMissing_MarksKeyFailed()
    {
        var (_, token) = await SignUpAndLoginAsync();
        var key = Guid.NewGuid();

        var response = await PostJsonAsync("api/wallet/fund",
            new { WalletId = Guid.NewGuid(), Amount = 100m }, key, token);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var stored = await GetIdempotencyAsync(key);
        Assert.NotNull(stored);
        Assert.Equal(IdempotencyStatus.Failed, stored.Status);
        Assert.Equal(404, stored.ResponseCode);
    }

    [Fact]
    public async Task WalletTransfer_Valid_ReturnsOk()
    {
        var (_, sourceToken) = await SignUpAndLoginAsync();
        var sourceWallet = await CreateWalletAsync(sourceToken, "Source");
        await FundAsync(sourceWallet.Id, 200m, sourceToken);

        var (_, destinationToken) = await SignUpAndLoginAsync();
        var destinationWallet = await CreateWalletAsync(destinationToken, "Destination");

        var response = await PostJsonAsync(
            $"api/wallet/{sourceWallet.Id}/transfer",
            new { DestinationWalletId = destinationWallet.Id, Amount = 100m },
            Guid.NewGuid(), sourceToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<WalletTransferResponse>(Json);
        Assert.NotNull(body);
        Assert.Equal(100m, body.Amount);
        Assert.Equal(100m, body.NewBalance);
        Assert.Equal(sourceWallet.Id, body.SourceWalletId);
        Assert.Equal(destinationWallet.Id, body.DestinationWalletId);
        Assert.Equal("Successful", body.State);
        _createdTransactionIds.Add(body.TransactionId);

        var sourceBalance = await GetBalanceAsync(sourceWallet.Id);
        var destinationBalance = await GetBalanceAsync(destinationWallet.Id);
        Assert.Equal(100m, sourceBalance);
        Assert.Equal(100m, destinationBalance);

        var ledgerRows = await QueryDbAsync(db => db.LedgerEntries.AsNoTracking()
            .Where(e => e.TransactionId == body.TransactionId)
            .ToListAsync());
        Assert.Equal(2, ledgerRows.Count);
        Assert.Contains(ledgerRows, e => e.WalletId == sourceWallet.Id && e.EntryType == LedgerEntryType.Debit && e.Amount == 100m);
        Assert.Contains(ledgerRows, e => e.WalletId == destinationWallet.Id && e.EntryType == LedgerEntryType.Credit && e.Amount == 100m);
    }

    [Fact]
    public async Task WalletTransfer_SameSourceAndDestination_ReturnsBadRequest()
    {
        var (_, token) = await SignUpAndLoginAsync();
        var wallet = await CreateWalletAsync(token, "Personal");

        var response = await PostJsonAsync(
            $"api/wallet/{wallet.Id}/transfer",
            new { DestinationWalletId = wallet.Id, Amount = 100m },
            Guid.NewGuid(), token);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task WalletTransfer_ExceedsBalance_ReturnsConflict()
    {
        var (_, sourceToken) = await SignUpAndLoginAsync();
        var sourceWallet = await CreateWalletAsync(sourceToken, "Source");
        await FundAsync(sourceWallet.Id, 100m, sourceToken);

        var (_, destinationToken) = await SignUpAndLoginAsync();
        var destinationWallet = await CreateWalletAsync(destinationToken, "Destination");

        var response = await PostJsonAsync(
            $"api/wallet/{sourceWallet.Id}/transfer",
            new { DestinationWalletId = destinationWallet.Id, Amount = 150m },
            Guid.NewGuid(), sourceToken);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        var sourceBalance = await GetBalanceAsync(sourceWallet.Id);
        var destinationBalance = await GetBalanceAsync(destinationWallet.Id);
        Assert.Equal(100m, sourceBalance);
        Assert.Equal(0m, destinationBalance);
    }

    [Fact]
    public async Task WalletTransfer_FromClosedWallet_ReturnsConflict()
    {
        var (_, sourceToken) = await SignUpAndLoginAsync();
        var sourceWallet = await CreateWalletAsync(sourceToken, "Source");

        var close = await PatchAsync($"api/wallet/{sourceWallet.Id}/close-wallet", sourceToken);
        Assert.Equal(HttpStatusCode.NoContent, close.StatusCode);

        var (_, destinationToken) = await SignUpAndLoginAsync();
        var destinationWallet = await CreateWalletAsync(destinationToken, "Destination");

        var response = await PostJsonAsync(
            $"api/wallet/{sourceWallet.Id}/transfer",
            new { DestinationWalletId = destinationWallet.Id, Amount = 100m },
            Guid.NewGuid(), sourceToken);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        var destinationBalance = await GetBalanceAsync(destinationWallet.Id);
        Assert.Equal(0m, destinationBalance);
    }

    [Fact]
    public async Task MerchantCheckout_Valid_ReturnsOk()
    {
        var (_, token) = await SignUpAndLoginAsync();
        var wallet = await CreateWalletAsync(token, "Spending");
        await FundAsync(wallet.Id, 200m, token);
        var merchantBalanceBefore = await GetBalanceAsync(_merchantWalletId);

        var response = await PostJsonAsync("api/wallet/checkout",
            new
            {
                UserWalletId = wallet.Id,
                MerchantWalletId = _merchantWalletId,
                OrderId = "ORDER-100",
                Amount = 100m
            },
            Guid.NewGuid(), token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<WalletCheckoutResponse>(Json);
        Assert.NotNull(body);
        Assert.Equal("ORDER-100", body.OrderId);
        Assert.Equal(100m, body.Amount);
        Assert.Equal(100m, body.NewBalance);
        Assert.Equal("Successful", body.State);
        _createdTransactionIds.Add(body.TransactionId);

        var userBalance = await GetBalanceAsync(wallet.Id);
        var merchantBalanceAfter = await GetBalanceAsync(_merchantWalletId);
        Assert.Equal(100m, userBalance);
        Assert.Equal(merchantBalanceBefore + 100m, merchantBalanceAfter);
    }

    [Fact]
    public async Task MerchantCheckout_ToNonMerchantWallet_ReturnsConflict()
    {
        var (_, token) = await SignUpAndLoginAsync();
        var userWallet = await CreateWalletAsync(token, "Spending");
        await FundAsync(userWallet.Id, 100m, token);

        var (_, otherToken) = await SignUpAndLoginAsync();
        var otherWallet = await CreateWalletAsync(otherToken, "NotMerchant");

        var response = await PostJsonAsync("api/wallet/checkout",
            new
            {
                UserWalletId = userWallet.Id,
                MerchantWalletId = otherWallet.Id,
                OrderId = "ORDER-NONMERCHANT",
                Amount = 100m
            },
            Guid.NewGuid(), token);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        var userBalance = await GetBalanceAsync(userWallet.Id);
        var otherBalance = await GetBalanceAsync(otherWallet.Id);
        Assert.Equal(100m, userBalance);
        Assert.Equal(0m, otherBalance);
    }

    [Fact]
    public async Task RefundPayment_Valid_ReturnsOk()
    {
        var (_, token) = await SignUpAndLoginAsync();
        var wallet = await CreateWalletAsync(token, "Spending");
        await FundAsync(wallet.Id, 200m, token);

        var checkout = await PostJsonAsync("api/wallet/checkout",
            new
            {
                UserWalletId = wallet.Id,
                MerchantWalletId = _merchantWalletId,
                OrderId = "ORDER-200",
                Amount = 100m
            },
            Guid.NewGuid(), token);
        Assert.Equal(HttpStatusCode.OK, checkout.StatusCode);
        var checkoutBody = await checkout.Content.ReadFromJsonAsync<WalletCheckoutResponse>(Json);
        _createdTransactionIds.Add(checkoutBody!.TransactionId);

        var response = await PostJsonAsync("api/wallet/refund",
            new
            {
                OriginalTransactionReference = checkoutBody.Reference,
                Reason = "Customer requested refund"
            },
            Guid.NewGuid(), token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<RefundResponse>(Json);
        Assert.NotNull(body);
        Assert.Equal(checkoutBody.Reference, body.OriginalTransactionReference);
        Assert.Equal(100m, body.Amount);
        Assert.Equal(wallet.Id, body.CreditedWalletId);
        Assert.False(body.CreatedWallet);
        Assert.Equal("Successful", body.State);
        _createdTransactionIds.Add(body.RefundTransactionId);

        var balance = await GetBalanceAsync(wallet.Id);
        Assert.Equal(200m, balance);
    }

    [Fact]
    public async Task RefundPayment_AlreadyRefunded_ReturnsConflict()
    {
        var (_, token) = await SignUpAndLoginAsync();
        var wallet = await CreateWalletAsync(token, "Spending");
        await FundAsync(wallet.Id, 300m, token);

        var checkout = await PostJsonAsync("api/wallet/checkout",
            new
            {
                UserWalletId = wallet.Id,
                MerchantWalletId = _merchantWalletId,
                OrderId = "ORDER-300",
                Amount = 200m
            },
            Guid.NewGuid(), token);
        Assert.Equal(HttpStatusCode.OK, checkout.StatusCode);
        var checkoutBody = await checkout.Content.ReadFromJsonAsync<WalletCheckoutResponse>(Json);
        _createdTransactionIds.Add(checkoutBody!.TransactionId);

        var first = await PostJsonAsync("api/wallet/refund",
            new
            {
                OriginalTransactionReference = checkoutBody.Reference,
                Reason = "Full refund"
            },
            Guid.NewGuid(), token);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var firstBody = await first.Content.ReadFromJsonAsync<RefundResponse>(Json);
        Assert.Equal(200m, firstBody!.Amount);
        _createdTransactionIds.Add(firstBody.RefundTransactionId);

        var second = await PostJsonAsync("api/wallet/refund",
            new
            {
                OriginalTransactionReference = checkoutBody.Reference,
                Reason = "Second refund"
            },
            Guid.NewGuid(), token);

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);

        var balance = await GetBalanceAsync(wallet.Id);
        Assert.Equal(300m, balance);

        var refundTotal = await QueryDbAsync(db => db.Transactions.AsNoTracking()
            .Where(t => t.ParentTransactionId == checkoutBody.TransactionId)
            .SumAsync(t => (decimal?)t.Amount)) ?? 0m;
        Assert.Equal(200m, refundTotal);
    }

    [Fact]
    public async Task RefundPayment_NonMerchantDestination_ReturnsBadRequest()
    {
        var (_, token) = await SignUpAndLoginAsync();
        var wallet = await CreateWalletAsync(token, "Spending");
        await FundAsync(wallet.Id, 200m, token);
        var otherWallet = await CreateWalletAsync(token, "Other");

        var payment = new Transaction
        {
            Reference = $"TXN-NONMERCH-{Guid.NewGuid():N}",
            Type = TransactionType.Payment,
            Status = TransactionStatus.Successful,
            Amount = 150m,
            SourceWalletId = wallet.Id,
            DestinationWalletId = otherWallet.Id
        };
        await QueryDbAsync(db =>
        {
            db.Transactions.Add(payment);
            return db.SaveChangesAsync();
        });
        _createdTransactionIds.Add(payment.Id);

        var response = await PostJsonAsync("api/wallet/refund",
            new
            {
                OriginalTransactionReference = payment.Reference,
                Reason = "Non-merchant destination"
            },
            Guid.NewGuid(), token);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var message = await response.Content.ReadAsStringAsync();
        Assert.Contains("Transaction is not refundable.", message);
    }

    [Fact]
    public async Task CloseWallet_WithBalance_ReturnsConflict()
    {
        var (_, token) = await SignUpAndLoginAsync();
        var wallet = await CreateWalletAsync(token, "ToClose");
        await FundAsync(wallet.Id, 100m, token);

        var response = await PatchAsync($"api/wallet/{wallet.Id}/close-wallet", token);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        var balance = await GetBalanceAsync(wallet.Id);
        Assert.Equal(100m, balance);
    }

    [Fact]
    public async Task CloseWallet_ZeroBalance_ReturnsNoContent()
    {
        var (_, token) = await SignUpAndLoginAsync();
        var wallet = await CreateWalletAsync(token, "ToClose");

        var response = await PatchAsync($"api/wallet/{wallet.Id}/close-wallet", token);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var stillExists = await QueryDbAsync(db => db.Wallets.AsNoTracking()
            .AnyAsync(w => w.Id == wallet.Id && w.Status == WalletStatus.Closed));
        Assert.True(stillExists);
    }

    [Fact]
    public async Task RefundPayment_ClosedSourceWallet_CreatesRefundWallet()
    {
        var (userId, token) = await SignUpAndLoginAsync();
        var wallet = await CreateWalletAsync(token, "Closing");
        await FundAsync(wallet.Id, 200m, token);

        var checkout1 = await PostJsonAsync("api/wallet/checkout",
            new
            {
                UserWalletId = wallet.Id,
                MerchantWalletId = _merchantWalletId,
                OrderId = "ORDER-CLOSE-1",
                Amount = 100m
            },
            Guid.NewGuid(), token);
        Assert.Equal(HttpStatusCode.OK, checkout1.StatusCode);
        var checkout1Body = await checkout1.Content.ReadFromJsonAsync<WalletCheckoutResponse>(Json);
        _createdTransactionIds.Add(checkout1Body!.TransactionId);

        var checkout2 = await PostJsonAsync("api/wallet/checkout",
            new
            {
                UserWalletId = wallet.Id,
                MerchantWalletId = _merchantWalletId,
                OrderId = "ORDER-CLOSE-2",
                Amount = 100m
            },
            Guid.NewGuid(), token);
        Assert.Equal(HttpStatusCode.OK, checkout2.StatusCode);
        var checkout2Body = await checkout2.Content.ReadFromJsonAsync<WalletCheckoutResponse>(Json);
        _createdTransactionIds.Add(checkout2Body!.TransactionId);

        var close = await PatchAsync($"api/wallet/{wallet.Id}/close-wallet", token);
        Assert.Equal(HttpStatusCode.NoContent, close.StatusCode);

        var response = await PostJsonAsync("api/wallet/refund",
            new
            {
                OriginalTransactionReference = checkout1Body.Reference,
                Reason = "Wallet closed"
            },
            Guid.NewGuid(), token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<RefundResponse>(Json);
        Assert.NotNull(body);
        Assert.True(body.CreatedWallet);
        Assert.Equal("Refund", body.CreditedWalletName);
        Assert.NotEqual(wallet.Id, body.CreditedWalletId);
        Assert.Equal(100m, body.Amount);
        Assert.Equal("Successful", body.State);
        _createdTransactionIds.Add(body.RefundTransactionId);

        var refundBalance = await GetBalanceAsync(body.CreditedWalletId);
        Assert.Equal(100m, refundBalance);

        var originalStatus = await QueryDbAsync(db => db.Wallets.AsNoTracking()
            .Where(w => w.Id == wallet.Id)
            .Select(w => w.Status)
            .SingleAsync());
        Assert.Equal(WalletStatus.Closed, originalStatus);

        var refundWalletOwner = await QueryDbAsync(db => db.Wallets.AsNoTracking()
            .Where(w => w.Id == body.CreditedWalletId)
            .Select(w => w.AccountId)
            .SingleAsync());
        Assert.Equal(userId, refundWalletOwner);

        var ledgerSum = await QueryDbAsync(db => db.LedgerEntries.AsNoTracking()
            .Where(e => e.WalletId == body.CreditedWalletId && e.EntryType == LedgerEntryType.Credit)
            .SumAsync(e => (decimal?)e.Amount)) ?? 0m;
        Assert.Equal(refundBalance, ledgerSum);
    }

    [Fact]
    public async Task RefundPayment_SecondRefundWhileSourceClosed_ReusesRefundWallet()
    {
        var (_, token) = await SignUpAndLoginAsync();
        var wallet = await CreateWalletAsync(token, "Closing");
        await FundAsync(wallet.Id, 200m, token);

        var checkout1 = await PostJsonAsync("api/wallet/checkout",
            new
            {
                UserWalletId = wallet.Id,
                MerchantWalletId = _merchantWalletId,
                OrderId = "ORDER-REUSE-1",
                Amount = 100m
            },
            Guid.NewGuid(), token);
        Assert.Equal(HttpStatusCode.OK, checkout1.StatusCode);
        var checkout1Body = await checkout1.Content.ReadFromJsonAsync<WalletCheckoutResponse>(Json);
        _createdTransactionIds.Add(checkout1Body!.TransactionId);

        var checkout2 = await PostJsonAsync("api/wallet/checkout",
            new
            {
                UserWalletId = wallet.Id,
                MerchantWalletId = _merchantWalletId,
                OrderId = "ORDER-REUSE-2",
                Amount = 100m
            },
            Guid.NewGuid(), token);
        Assert.Equal(HttpStatusCode.OK, checkout2.StatusCode);
        var checkout2Body = await checkout2.Content.ReadFromJsonAsync<WalletCheckoutResponse>(Json);
        _createdTransactionIds.Add(checkout2Body!.TransactionId);

        var close = await PatchAsync($"api/wallet/{wallet.Id}/close-wallet", token);
        Assert.Equal(HttpStatusCode.NoContent, close.StatusCode);

        var firstRefund = await PostJsonAsync("api/wallet/refund",
            new
            {
                OriginalTransactionReference = checkout1Body.Reference,
                Reason = "First refund"
            },
            Guid.NewGuid(), token);
        Assert.Equal(HttpStatusCode.OK, firstRefund.StatusCode);
        var firstRefundBody = await firstRefund.Content.ReadFromJsonAsync<RefundResponse>(Json);
        Assert.True(firstRefundBody!.CreatedWallet);
        _createdTransactionIds.Add(firstRefundBody.RefundTransactionId);

        var merchantBalanceBefore = await GetBalanceAsync(_merchantWalletId);

        var response = await PostJsonAsync("api/wallet/refund",
            new
            {
                OriginalTransactionReference = checkout2Body.Reference,
                Reason = "Second refund"
            },
            Guid.NewGuid(), token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<RefundResponse>(Json);
        Assert.NotNull(body);
        Assert.False(body.CreatedWallet);
        Assert.Equal(firstRefundBody.CreditedWalletId, body.CreditedWalletId);
        Assert.Equal("Refund", body.CreditedWalletName);
        Assert.Equal("Successful", body.State);
        _createdTransactionIds.Add(body.RefundTransactionId);

        var refundBalance = await GetBalanceAsync(body.CreditedWalletId);
        Assert.Equal(200m, refundBalance);

        var merchantBalanceAfter = await GetBalanceAsync(_merchantWalletId);
        Assert.Equal(merchantBalanceBefore - 100m, merchantBalanceAfter);

        var ledgerSum = await QueryDbAsync(db => db.LedgerEntries.AsNoTracking()
            .Where(e => e.WalletId == body.CreditedWalletId && e.EntryType == LedgerEntryType.Credit)
            .SumAsync(e => (decimal?)e.Amount)) ?? 0m;
        Assert.Equal(200m, ledgerSum);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("99")]
    [InlineData("-100")]
    [InlineData("10000001")]
    public async Task FundWallet_AmountOutOfRange_ReturnsBadRequest(string amount)
    {
        var (_, token) = await SignUpAndLoginAsync();

        var response = await PostJsonAsync("api/wallet/fund",
            new { WalletId = Guid.NewGuid(), Amount = decimal.Parse(amount, CultureInfo.InvariantCulture) }, token: token);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("99")]
    [InlineData("-100")]
    [InlineData("10000001")]
    public async Task WalletTransfer_AmountOutOfRange_ReturnsBadRequest(string amount)
    {
        var (_, token) = await SignUpAndLoginAsync();

        var response = await PostJsonAsync(
            $"api/wallet/{Guid.NewGuid()}/transfer",
            new { DestinationWalletId = Guid.NewGuid(), Amount = decimal.Parse(amount, CultureInfo.InvariantCulture) }, token: token);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("99")]
    [InlineData("-100")]
    [InlineData("10000001")]
    public async Task MerchantCheckout_AmountOutOfRange_ReturnsBadRequest(string amount)
    {
        var (_, token) = await SignUpAndLoginAsync();

        var response = await PostJsonAsync("api/wallet/checkout",
            new
            {
                UserWalletId = Guid.NewGuid(),
                MerchantWalletId = Guid.NewGuid(),
                OrderId = "ORDER-RANGE",
                Amount = decimal.Parse(amount, CultureInfo.InvariantCulture)
            }, token: token);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task WalletTransfer_AmountWithMoreThan2Decimals_ReturnsBadRequest()
    {
        var (_, token) = await SignUpAndLoginAsync();

        var response = await PostJsonAsync(
            $"api/wallet/{Guid.NewGuid()}/transfer",
            new { DestinationWalletId = Guid.NewGuid(), Amount = 100.123m },
            Guid.NewGuid(), token);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task MerchantCheckout_AmountWithMoreThan2Decimals_ReturnsBadRequest()
    {
        var (_, token) = await SignUpAndLoginAsync();

        var response = await PostJsonAsync("api/wallet/checkout",
            new
            {
                UserWalletId = Guid.NewGuid(),
                MerchantWalletId = Guid.NewGuid(),
                OrderId = "ORDER-DECIMALS",
                Amount = 100.123m
            },
            Guid.NewGuid(), token);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task MerchantWallet_BalanceMatchesLedger()
    {
        var (_, token) = await SignUpAndLoginAsync();
        var wallet = await CreateWalletAsync(token, "Spending");
        await FundAsync(wallet.Id, 300m, token);

        var checkout = await PostJsonAsync("api/wallet/checkout",
            new
            {
                UserWalletId = wallet.Id,
                MerchantWalletId = _merchantWalletId,
                OrderId = "ORDER-AUDIT",
                Amount = 100m
            },
            Guid.NewGuid(), token);
        checkout.EnsureSuccessStatusCode();
        var checkoutBody = await checkout.Content.ReadFromJsonAsync<WalletCheckoutResponse>(Json);
        _createdTransactionIds.Add(checkoutBody!.TransactionId);

        var refund = await PostJsonAsync("api/wallet/refund",
            new
            {
                OriginalTransactionReference = checkoutBody.Reference,
                Reason = "Audit refund"
            },
            Guid.NewGuid(), token);
        refund.EnsureSuccessStatusCode();
        var refundBody = await refund.Content.ReadFromJsonAsync<RefundResponse>(Json);
        _createdTransactionIds.Add(refundBody!.RefundTransactionId);

        var stored = await GetBalanceAsync(_merchantWalletId);

        var credits = await QueryDbAsync(db => db.LedgerEntries.AsNoTracking()
            .Where(e => e.WalletId == _merchantWalletId && e.EntryType == LedgerEntryType.Credit)
            .SumAsync(e => (decimal?)e.Amount)) ?? 0m;

        var debits = await QueryDbAsync(db => db.LedgerEntries.AsNoTracking()
            .Where(e => e.WalletId == _merchantWalletId && e.EntryType == LedgerEntryType.Debit)
            .SumAsync(e => (decimal?)e.Amount)) ?? 0m;

        Assert.Equal(credits - debits, stored);
        Assert.Equal(0m, stored);
    }
}
