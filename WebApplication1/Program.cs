using BankingPlatform.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<BankingStore>();
builder.Services.AddSingleton<BankingService>();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHealthChecks();
builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
    policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();

app.UseCors();
app.UseSwagger();
app.UseSwaggerUI();
app.MapHealthChecks("/health");
app.MapGet("/", () => Results.Ok(new
{
    service = "Banking Platform API",
    version = "1.0",
    services = new[] { "identity", "customers", "accounts", "transactions", "payments", "loans", "notifications", "reports" }
}));

var api = app.MapGroup("/api");

var identity = api.MapGroup("/identity").WithTags("Identity");
identity.MapPost("/login", (LoginRequest request, BankingService service) =>
{
    var result = service.Login(request);
    return result is null ? Results.Unauthorized() : Results.Ok(result);
});
identity.MapPost("/register", (RegisterRequest request, BankingService service) =>
    Results.Created("/api/customers", service.Register(request)));

var customers = api.MapGroup("/customers").WithTags("Customers");
customers.MapGet("/", (BankingService service) => Results.Ok(service.GetCustomers()));
customers.MapGet("/{id:guid}", (Guid id, BankingService service) =>
    service.GetCustomer(id) is { } customer ? Results.Ok(customer) : Results.NotFound());
customers.MapPost("/", (CreateCustomerRequest request, BankingService service) =>
    Results.Created($"/api/customers/{request.Id}", service.CreateCustomer(request)));

var accounts = api.MapGroup("/accounts").WithTags("Accounts");
accounts.MapGet("/{id:guid}", (Guid id, BankingService service) =>
    service.GetAccount(id) is { } account ? Results.Ok(account) : Results.NotFound());
accounts.MapGet("/customer/{customerId:guid}", (Guid customerId, BankingService service) =>
    Results.Ok(service.GetAccounts(customerId)));
accounts.MapPost("/", (CreateAccountRequest request, BankingService service) =>
{
    var account = service.CreateAccount(request);
    return account is null ? Results.BadRequest("Customer does not exist.") : Results.Created($"/api/accounts/{account.Id}", account);
});
accounts.MapPost("/{id:guid}/deposit", (Guid id, DepositRequest request, BankingService service) =>
    service.Deposit(id, request) is { } account ? Results.Ok(account) : Results.BadRequest("Account does not exist or amount is invalid."));

var transactions = api.MapGroup("/transactions").WithTags("Transactions");
transactions.MapGet("/account/{accountId:guid}", (Guid accountId, BankingService service) =>
    Results.Ok(service.GetTransactions(accountId)));
transactions.MapPost("/transfer", (TransferRequest request, BankingService service) =>
{
    var result = service.Transfer(request);
    return result.Success ? Results.Ok(result) : Results.BadRequest(result.Message);
});

var loans = api.MapGroup("/loans").WithTags("Loans");
loans.MapGet("/customer/{customerId:guid}", (Guid customerId, BankingService service) =>
    Results.Ok(service.GetLoans(customerId)));
loans.MapPost("/", (CreateLoanRequest request, BankingService service) =>
    Results.Created("/api/loans", service.CreateLoan(request)));

var payments = api.MapGroup("/payments").WithTags("Payments");
payments.MapPost("/", (PaymentRequest request, BankingService service) =>
{
    var result = service.Transfer(new TransferRequest(request.FromAccountId, request.ToAccountId, request.Amount, request.Description));
    return result.Success
        ? Results.Ok(new { paymentId = Guid.NewGuid(), status = "Completed", result.Message })
        : Results.BadRequest(new { status = "Failed", result.Message });
});

var notifications = api.MapGroup("/notifications").WithTags("Notifications");
notifications.MapGet("/customer/{customerId:guid}", (Guid customerId, BankingService service) =>
    Results.Ok(service.GetNotifications(customerId)));

var reports = api.MapGroup("/reports").WithTags("Reports");
reports.MapGet("/customer/{customerId:guid}/statement", (Guid customerId, BankingService service) =>
    Results.Ok(service.GetStatement(customerId)));

app.Run();
