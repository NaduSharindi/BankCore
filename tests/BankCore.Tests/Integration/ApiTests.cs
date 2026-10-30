using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using BankCore.Domain.Common;

namespace BankCore.Tests.Integration;

public sealed class ApiTests(BankCoreApiFactory factory) : IClassFixture<BankCoreApiFactory>
{
    // Seeded demo accounts (see DbInitializer)
    private const string KasunSavings = "001000000016";
    private const string NimaliSavings = "001000000024";
    private const string KasunCurrent = "001000000032";

    [Fact]
    public async Task Health_endpoint_is_public()
    {
        var response = await factory.CreateClient().GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Api_endpoints_require_a_token()
    {
        var response = await factory.CreateClient().GetAsync($"/api/v1/accounts/{KasunSavings}");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Wrong_password_returns_401_problem_details_with_generic_message()
    {
        var response = await factory.CreateClient().PostAsJsonAsync(
            "/api/v1/auth/login", new { username = "ghost", password = "wrong-password" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDto>();
        Assert.Equal("AUTHENTICATION_FAILED", problem!.ErrorCode);
        Assert.Equal("Invalid username or password.", problem.Detail);
    }

    [Fact]
    public async Task Responses_carry_security_and_correlation_headers()
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Correlation-ID", "test-trace-123");

        var response = await client.GetAsync($"/api/v1/accounts/{KasunSavings}");

        Assert.Equal("test-trace-123", response.Headers.GetValues("X-Correlation-ID").Single());
        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
    }

    [Fact]
    public async Task Teller_deposit_appears_in_statement_and_balance_reconciles()
    {
        var client = await LoginAsync("teller");
        var narration = $"Integration deposit {Guid.NewGuid():N}";

        var deposit = await client.PostAsJsonAsync(
            $"/api/v1/accounts/{NimaliSavings}/deposits", new { amount = 5_000.75m, narration });
        Assert.Equal(HttpStatusCode.Created, deposit.StatusCode);

        var statement = await client.GetFromJsonAsync<StatementDto>(
            $"/api/v1/accounts/{NimaliSavings}/statement?pageSize=100");
        var account = await client.GetFromJsonAsync<AccountDto>($"/api/v1/accounts/{NimaliSavings}");

        Assert.Contains(statement!.Transactions.Items, t => t.Narration == narration && t.Amount == 5_000.75m);
        Assert.Equal(account!.Balance, statement.ClosingBalance);
    }

    [Fact]
    public async Task Transfer_requires_idempotency_key()
    {
        var client = await LoginAsync("teller");

        var response = await client.PostAsJsonAsync("/api/v1/transfers", TransferBody(100m));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Transfer_is_safe_to_retry_with_the_same_idempotency_key()
    {
        var client = await LoginAsync("teller");
        var before = await client.GetFromJsonAsync<AccountDto>($"/api/v1/accounts/{KasunSavings}");
        var key = Guid.NewGuid().ToString();

        var first = await SendTransferAsync(client, key, 1_250m);
        var retry = await SendTransferAsync(client, key, 1_250m);
        var after = await client.GetFromJsonAsync<AccountDto>($"/api/v1/accounts/{KasunSavings}");

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, retry.StatusCode);
        Assert.Equal(before!.Balance - 1_250m, after!.Balance);
    }

    [Fact]
    public async Task Overdrawing_returns_422()
    {
        var client = await LoginAsync("teller");

        var response = await client.PostAsJsonAsync(
            $"/api/v1/accounts/{NimaliSavings}/withdrawals", new { amount = 9_999_999m, narration = "too much" });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDto>();
        Assert.Equal("INSUFFICIENT_FUNDS", problem!.ErrorCode);
    }

    [Theory]
    [InlineData("auditor")]
    [InlineData("admin")]
    public async Task Only_tellers_can_move_money(string username)
    {
        var client = await LoginAsync(username);

        var response = await client.PostAsJsonAsync(
            $"/api/v1/accounts/{KasunSavings}/deposits", new { amount = 100m, narration = "not allowed" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Teller_cannot_read_the_audit_trail_but_auditor_can()
    {
        var teller = await LoginAsync("teller");
        var auditor = await LoginAsync("auditor");

        Assert.Equal(HttpStatusCode.Forbidden, (await teller.GetAsync("/api/v1/audit-logs")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await auditor.GetAsync("/api/v1/audit-logs")).StatusCode);
    }

    [Fact]
    public async Task Teller_can_onboard_a_customer_and_open_an_account()
    {
        var client = await LoginAsync("teller");
        var nic = $"1995100{Random.Shared.Next(10_000, 99_999)}";

        var customerResponse = await client.PostAsJsonAsync("/api/v1/customers", new
        {
            fullName = "Integration Test Customer",
            nic,
            email = "it.customer@example.com",
            phoneNumber = "0771234567",
        });
        Assert.Equal(HttpStatusCode.Created, customerResponse.StatusCode);
        var customer = await customerResponse.Content.ReadFromJsonAsync<CustomerDto>();

        var accountResponse = await client.PostAsJsonAsync("/api/v1/accounts", new
        {
            customerId = customer!.Id,
            type = "Savings",
            initialDeposit = 2_500m,
        });
        Assert.Equal(HttpStatusCode.Created, accountResponse.StatusCode);
        var account = await accountResponse.Content.ReadFromJsonAsync<AccountDto>();

        Assert.True(Luhn.IsValid(account!.AccountNumber));
        Assert.Equal(2_500m, account.Balance);

        var duplicate = await client.PostAsJsonAsync("/api/v1/customers", new
        {
            fullName = "Someone Else",
            nic,
            email = "someone@example.com",
            phoneNumber = "0771234567",
        });
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
    }

    [Fact]
    public async Task Invalid_nic_is_rejected_by_model_validation()
    {
        var client = await LoginAsync("teller");

        var response = await client.PostAsJsonAsync("/api/v1/customers", new
        {
            fullName = "Bad Nic",
            nic = "199040012345",
            email = "bad.nic@example.com",
            phoneNumber = "0771234567",
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private async Task<HttpClient> LoginAsync(string username)
    {
        var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync(
            "/api/v1/auth/login", new { username, password = BankCoreApiFactory.Password });
        response.EnsureSuccessStatusCode();

        var token = await response.Content.ReadFromJsonAsync<TokenDto>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token!.Token);
        return client;
    }

    private static object TransferBody(decimal amount) => new
    {
        fromAccountNumber = KasunSavings,
        toAccountNumber = KasunCurrent,
        amount,
        narration = "Integration transfer",
    };

    private static Task<HttpResponseMessage> SendTransferAsync(HttpClient client, string key, decimal amount)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/transfers")
        {
            Content = JsonContent.Create(TransferBody(amount)),
        };
        request.Headers.Add("Idempotency-Key", key);
        return client.SendAsync(request);
    }

    // Minimal response shapes - only the fields the tests assert on.
    private sealed record TokenDto(string Token);

    private sealed record ProblemDto(string? Detail, string? ErrorCode);

    private sealed record AccountDto(string AccountNumber, decimal Balance);

    private sealed record CustomerDto(Guid Id);

    private sealed record TransactionDto(string? Narration, decimal Amount);

    private sealed record PageDto(List<TransactionDto> Items);

    private sealed record StatementDto(decimal ClosingBalance, PageDto Transactions);
}
