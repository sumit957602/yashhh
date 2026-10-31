using System.Security.Cryptography;

namespace BankingPlatform.Api.Services;

public sealed class BankingService(BankingStore store, ILogger<BankingService> logger)
{
    public Customer Register(RegisterRequest request)
    {
        ValidateText(request.FullName, 2, 120, "Full name");
        ValidateEmail(request.Email);
        ValidateText(request.Password, 12, 128, "Password");
        var email = NormalizeEmail(request.Email);
        if (store.Credentials.ContainsKey(email)) throw new InvalidOperationException("An account with this email already exists.");

        var customer = CreateCustomer(new CreateCustomerRequest(request.FullName.Trim(), email, string.Empty));
        if (!store.Credentials.TryAdd(email, (HashPassword(request.Password), customer.Id)))
        {
            store.Customers.TryRemove(customer.Id, out _);
            throw new InvalidOperationException("An account with this email already exists.");
        }
        return customer;
    }

    public LoginResponse? Login(LoginRequest request)
    {
        var email = NormalizeEmail(request.Email);
        if (!store.Credentials.TryGetValue(email, out var credential) ||
            !VerifyPassword(request.Password, credential.PasswordHash) ||
            !store.Customers.TryGetValue(credential.CustomerId, out var customer))
            return null;

        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        store.Sessions[token] = (customer.Id, DateTime.UtcNow.AddMinutes(30));
        logger.LogInformation("Successful login for customer {CustomerId}", customer.Id);
        return new LoginResponse(token, customer);
    }

    public Guid? GetSessionCustomer(string token)
    {
        if (!store.Sessions.TryGetValue(token, out var session)) return null;
        if (session.ExpiresAt <= DateTime.UtcNow)
        {
            store.Sessions.TryRemove(token, out _);
            return null;
        }
        return session.CustomerId;
    }

    public Customer CreateCustomer(CreateCustomerRequest request)
    {
        ValidateText(request.FullName, 2, 120, "Full name");
        ValidateEmail(request.Email);
        var customer = new Customer(request.Id, request.FullName.Trim(), NormalizeEmail(request.Email), request.Phone.Trim(), DateTime.UtcNow);
        store.Customers[customer.Id] = customer;
        return customer;
    }

    public Customer? GetCustomer(Guid id, Guid ownerId) => id == ownerId ? store.Customers.GetValueOrDefault(id) : null;
    public IEnumerable<Customer> GetCustomers(Guid ownerId) => store.Customers.Values.Where(x => x.Id == ownerId);
    public Account? GetAccount(Guid id, Guid ownerId) => store.Accounts.TryGetValue(id, out var account) && account.CustomerId == ownerId ? account : null;
    public IEnumerable<Account> GetAccounts(Guid customerId, Guid ownerId) => customerId == ownerId ? store.Accounts.Values.Where(x => x.CustomerId == customerId) : [];
    public IEnumerable<Transaction> GetTransactions(Guid accountId, Guid ownerId) =>
        GetAccount(accountId, ownerId) is null ? [] : store.Transactions.Values.Where(x => x.AccountId == accountId).OrderByDescending(x => x.CreatedAt);
    public IEnumerable<Loan> GetLoans(Guid customerId, Guid ownerId) => customerId == ownerId ? store.Loans.Values.Where(x => x.CustomerId == customerId) : [];
    public IEnumerable<Notification> GetNotifications(Guid customerId, Guid ownerId) =>
        customerId == ownerId ? store.Notifications.Values.Where(x => x.CustomerId == customerId).OrderByDescending(x => x.CreatedAt) : [];

    public Account? CreateAccount(CreateAccountRequest request, Guid ownerId)
    {
        if (request.CustomerId != ownerId || !store.Customers.ContainsKey(ownerId) ||
            !IsAllowedAccountType(request.Type) || !IsAllowedCurrency(request.Currency)) return null;
        var account = new Account(Guid.NewGuid(), ownerId, $"10{Random.Shared.Next(10000000, 99999999)}",
            request.Type.Trim(), request.Currency.Trim().ToUpperInvariant(), 0, "Active");
        store.Accounts[account.Id] = account;
        return account;
    }

    public Account? Deposit(Guid accountId, DepositRequest request, Guid ownerId)
    {
        if (request.Amount <= 0 || request.Amount > 1_000_000 || !store.Accounts.TryGetValue(accountId, out var account) || account.CustomerId != ownerId) return null;
        lock (store.TransferLock)
        {
            var updated = account with { Balance = account.Balance + request.Amount };
            store.Accounts[accountId] = updated;
            var transaction = new Transaction(Guid.NewGuid(), accountId, request.Amount, "Deposit", "Cash deposit", DateTime.UtcNow);
            store.Transactions[transaction.Id] = transaction;
            return updated;
        }
    }

    public TransferResult Transfer(TransferRequest request, Guid ownerId)
    {
        if (request.Amount <= 0 || request.Amount > 1_000_000) return new(false, "Amount must be between 0 and 1,000,000.", null, null);
        if (!store.Accounts.TryGetValue(request.FromAccountId, out var source) ||
            !store.Accounts.TryGetValue(request.ToAccountId, out var destination) ||
            source.CustomerId != ownerId || destination.CustomerId != ownerId)
            return new(false, "You can only transfer between your own accounts.", null, null);
        lock (store.TransferLock)
        {
            if (!store.Accounts.TryGetValue(request.FromAccountId, out source) || !store.Accounts.TryGetValue(request.ToAccountId, out destination) || source.Balance < request.Amount)
                return new(false, "Insufficient funds.", null, null);
            var now = DateTime.UtcNow;
            var description = string.IsNullOrWhiteSpace(request.Description) ? "Account transfer" : request.Description.Trim()[..Math.Min(request.Description.Trim().Length, 200)];
            var debit = new Transaction(Guid.NewGuid(), source.Id, -request.Amount, "Transfer", description, now);
            var credit = new Transaction(Guid.NewGuid(), destination.Id, request.Amount, "Transfer", description, now);
            store.Accounts[source.Id] = source with { Balance = source.Balance - request.Amount };
            store.Accounts[destination.Id] = destination with { Balance = destination.Balance + request.Amount };
            store.Transactions[debit.Id] = debit;
            store.Transactions[credit.Id] = credit;
            var notification = new Notification(Guid.NewGuid(), ownerId, $"Transfer of {request.Amount} sent.", now);
            store.Notifications[notification.Id] = notification;
            logger.LogInformation("Transfer completed for customer {CustomerId}, amount {Amount}", ownerId, request.Amount);
            return new(true, "Transfer completed.", debit, credit);
        }
    }

    public Loan? CreateLoan(CreateLoanRequest request, Guid ownerId)
    {
        if (request.CustomerId != ownerId || request.Amount <= 0 || request.Amount > 10_000_000 || request.TermMonths is < 3 or > 360) return null;
        var loan = new Loan(Guid.NewGuid(), ownerId, request.Amount, request.TermMonths, "Pending", DateTime.UtcNow);
        store.Loans[loan.Id] = loan;
        return loan;
    }

    public Statement GetStatement(Guid customerId, Guid ownerId) => customerId != ownerId
        ? new(customerId, [], [])
        : new(customerId, GetAccounts(customerId, ownerId).ToList(), GetAccounts(customerId, ownerId).SelectMany(a => GetTransactions(a.Id, ownerId)).OrderByDescending(t => t.CreatedAt).ToList());

    private static string NormalizeEmail(string? email) => email?.Trim().ToLowerInvariant() ?? string.Empty;
    private static void ValidateEmail(string email) { if (email is null || email.Length > 254 || !email.Contains('@') || email.Contains(' ')) throw new ArgumentException("A valid email is required."); }
    private static void ValidateText(string value, int min, int max, string name) { if (string.IsNullOrWhiteSpace(value) || value.Trim().Length < min || value.Trim().Length > max) throw new ArgumentException($"{name} must be between {min} and {max} characters."); }
    private static bool IsAllowedAccountType(string type) => type.Equals("Savings", StringComparison.OrdinalIgnoreCase) || type.Equals("Current", StringComparison.OrdinalIgnoreCase);
    private static bool IsAllowedCurrency(string currency) => currency.Equals("INR", StringComparison.OrdinalIgnoreCase) || currency.Equals("USD", StringComparison.OrdinalIgnoreCase);
    private static string HashPassword(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, 120_000, HashAlgorithmName.SHA256, 32);
        return $"{Convert.ToBase64String(salt)}.{Convert.ToBase64String(hash)}";
    }
    private static bool VerifyPassword(string password, string stored)
    {
        var parts = stored.Split('.');
        if (parts.Length != 2) return false;
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, Convert.FromBase64String(parts[0]), 120_000, HashAlgorithmName.SHA256, 32);
        return CryptographicOperations.FixedTimeEquals(hash, Convert.FromBase64String(parts[1]));
    }
}
