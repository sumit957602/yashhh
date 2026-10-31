using BankingPlatform.Api.Services;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton<BankingStore>();
builder.Services.AddSingleton<BankingService>();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHealthChecks();
builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
    policy.WithOrigins("http://localhost:4200", "http://localhost:4201")
        .AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();
if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
    app.UseHttpsRedirection();
}
app.UseExceptionHandler(exceptionApp => exceptionApp.Run(async context =>
{
    context.Response.StatusCode = StatusCodes.Status500InternalServerError;
    await context.Response.WriteAsJsonAsync(new { error = "An unexpected error occurred." });
}));
app.Use(async (context, next) =>
{
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["X-Frame-Options"] = "DENY";
    context.Response.Headers["Referrer-Policy"] = "no-referrer";
    if (context.Request.Path.StartsWithSegments("/api") &&
        !context.Request.Path.StartsWithSegments("/api/identity"))
    {
        var header = context.Request.Headers.Authorization.ToString();
        if (!header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new { error = "Authentication is required." });
            return;
        }
        var token = header["Bearer ".Length..].Trim();
        var service = context.RequestServices.GetRequiredService<BankingService>();
        var customerId = service.GetSessionCustomer(token);
        if (customerId is null)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new { error = "The session is invalid or expired." });
            return;
        }
        context.Items["CustomerId"] = customerId.Value;
    }
    await next();
});
app.UseCors();
app.UseSwagger();
app.UseSwaggerUI();
app.MapHealthChecks("/health");
app.MapGet("/", () => Results.Ok(new { service = "Banking Platform API", version = "1.0" }));

static Guid Owner(HttpContext context) => (Guid)context.Items["CustomerId"]!;
var api = app.MapGroup("/api");
var identity = api.MapGroup("/identity").WithTags("Identity");
identity.MapPost("/login", (LoginRequest request, BankingService service) =>
    service.Login(request) is { } result ? Results.Ok(result) : Results.Unauthorized());
identity.MapPost("/register", (RegisterRequest request, BankingService service) =>
{
    try { return Results.Created("/api/identity/login", service.Register(request)); }
    catch (ArgumentException e) { return Results.BadRequest(new { error = e.Message }); }
    catch (InvalidOperationException e) { return Results.Conflict(new { error = e.Message }); }
});

var customers = api.MapGroup("/customers").WithTags("Customers");
customers.MapGet("/", (HttpContext http, BankingService service) => Results.Ok(service.GetCustomers(Owner(http))));
customers.MapGet("/{id:guid}", (Guid id, HttpContext http, BankingService service) =>
    service.GetCustomer(id, Owner(http)) is { } customer ? Results.Ok(customer) : Results.NotFound());

var accounts = api.MapGroup("/accounts").WithTags("Accounts");
accounts.MapGet("/{id:guid}", (Guid id, HttpContext http, BankingService service) =>
    service.GetAccount(id, Owner(http)) is { } account ? Results.Ok(account) : Results.NotFound());
accounts.MapGet("/customer/{customerId:guid}", (Guid customerId, HttpContext http, BankingService service) => Results.Ok(service.GetAccounts(customerId, Owner(http))));
accounts.MapPost("/", (CreateAccountRequest request, HttpContext http, BankingService service) =>
    service.CreateAccount(request, Owner(http)) is { } account ? Results.Created($"/api/accounts/{account.Id}", account) : Results.BadRequest(new { error = "Invalid account request." }));
accounts.MapPost("/{id:guid}/deposit", (Guid id, DepositRequest request, HttpContext http, BankingService service) =>
    service.Deposit(id, request, Owner(http)) is { } account ? Results.Ok(account) : Results.BadRequest(new { error = "Account does not exist or amount is invalid." }));

var transactions = api.MapGroup("/transactions").WithTags("Transactions");
transactions.MapGet("/account/{accountId:guid}", (Guid accountId, HttpContext http, BankingService service) => Results.Ok(service.GetTransactions(accountId, Owner(http))));
transactions.MapPost("/transfer", (TransferRequest request, HttpContext http, BankingService service) =>
{
    var result = service.Transfer(request, Owner(http));
    return result.Success ? Results.Ok(result) : Results.BadRequest(new { error = result.Message });
});

var loans = api.MapGroup("/loans").WithTags("Loans");
loans.MapGet("/customer/{customerId:guid}", (Guid customerId, HttpContext http, BankingService service) => Results.Ok(service.GetLoans(customerId, Owner(http))));
loans.MapPost("/", (CreateLoanRequest request, HttpContext http, BankingService service) =>
    service.CreateLoan(request, Owner(http)) is { } loan ? Results.Created("/api/loans", loan) : Results.BadRequest(new { error = "Invalid loan request." }));

var payments = api.MapGroup("/payments").WithTags("Payments");
payments.MapPost("/", (PaymentRequest request, HttpContext http, BankingService service) =>
{
    var result = service.Transfer(new TransferRequest(request.FromAccountId, request.ToAccountId, request.Amount, request.Description), Owner(http));
    return result.Success ? Results.Ok(new { paymentId = Guid.NewGuid(), status = "Completed" }) : Results.BadRequest(new { error = result.Message });
});

var notifications = api.MapGroup("/notifications").WithTags("Notifications");
notifications.MapGet("/customer/{customerId:guid}", (Guid customerId, HttpContext http, BankingService service) => Results.Ok(service.GetNotifications(customerId, Owner(http))));
var reports = api.MapGroup("/reports").WithTags("Reports");
reports.MapGet("/customer/{customerId:guid}/statement", (Guid customerId, HttpContext http, BankingService service) => Results.Ok(service.GetStatement(customerId, Owner(http))));
app.Run();
