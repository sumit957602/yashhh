namespace BankingPlatform.Api.Services;

public sealed class BankingService(BankingStore store)
{
    public Customer Register(RegisterRequest request)
    {
        var customer = CreateCustomer(new CreateCustomerRequest(request.FullName, request.Email, string.Empty));
        store.Credentials[request.Email.ToLowerInvariant()] = (request.Password, customer.Id);
        return customer;
    }

    public LoginResponse? Login(LoginRequest request)
    {
        if (!store.Credentials.TryGetValue(request.Email.ToLowerInvariant(), out var credential) ||
            credential.Password != request.Password ||
            !store.Customers.TryGetValue(credential.CustomerId, out var customer))
            return null;

        return new LoginResponse(Convert.ToBase64String(Guid.NewGuid().ToByteArray()), customer);
    }

    public Customer CreateCustomer(CreateCustomerRequest request)
    {
        var customer = new Customer(request.Id, request.FullName, request.Email, request.Phone, DateTime.UtcNow);
        store.Customers[customer.Id] = customer;
        return customer;
    }

    public IEnumerable<Customer> GetCustomers() => store.Customers.Values.OrderBy(x => x.FullName);
    public Customer? GetCustomer(Guid id) => store.Customers.GetValueOrDefault(id);
    public Account? GetAccount(Guid id) => store.Accounts.GetValueOrDefault(id);
    public IEnumerable<Account> GetAccounts(Guid customerId) => store.Accounts.Values.Where(x => x.CustomerId == customerId);
    public IEnumerable<Transaction> GetTransactions(Guid accountId) =>
        store.Transactions.Values.Where(x => x.AccountId == accountId).OrderByDescending(x => x.CreatedAt);
    public IEnumerable<Loan> GetLoans(Guid customerId) => store.Loans.Values.Where(x => x.CustomerId == customerId);
    public IEnumerable<Notification> GetNotifications(Guid customerId) =>
        store.Notifications.Values.Where(x => x.CustomerId == customerId).OrderByDescending(x => x.CreatedAt);

    public Account? CreateAccount(CreateAccountRequest request)
    {
        if (!store.Customers.ContainsKey(request.CustomerId)) return null;
        var account = new Account(Guid.NewGuid(), request.CustomerId,
            $"10{Random.Shared.Next(10000000, 99999999)}", request.Type, request.Currency, 0, "Active");
        store.Accounts[account.Id] = account;
        return account;
    }

    public Account? Deposit(Guid accountId, DepositRequest request)
    {
        if (request.Amount <= 0 || !store.Accounts.TryGetValue(accountId, out var account)) return null;
        var updated = account with { Balance = account.Balance + request.Amount };
        store.Accounts[accountId] = updated;
        var transaction = new Transaction(Guid.NewGuid(), accountId, request.Amount, "Deposit", "Cash deposit", DateTime.UtcNow);
        store.Transactions[transaction.Id] = transaction;
        return updated;
    }

    public TransferResult Transfer(TransferRequest request)
    {
        if (request.Amount <= 0) return new(false, "Amount must be greater than zero.", null, null);
        if (!store.Accounts.TryGetValue(request.FromAccountId, out var sourceAccount) ||
            !store.Accounts.TryGetValue(request.ToAccountId, out var destinationAccount))
            return new(false, "One or both accounts do not exist.", null, null);
        if (sourceAccount.Balance < request.Amount) return new(false, "Insufficient funds.", null, null);

        var now = DateTime.UtcNow;
        var debit = new Transaction(Guid.NewGuid(), sourceAccount.Id, -request.Amount, "Transfer", request.Description ?? "Account transfer", now);
        var credit = new Transaction(Guid.NewGuid(), destinationAccount.Id, request.Amount, "Transfer", request.Description ?? "Account transfer", now);
        var updatedFrom = sourceAccount with { Balance = sourceAccount.Balance - request.Amount };
        var updatedTo = destinationAccount with { Balance = destinationAccount.Balance + request.Amount };
        store.Accounts[sourceAccount.Id] = updatedFrom;
        store.Accounts[destinationAccount.Id] = updatedTo;
        store.Transactions[debit.Id] = debit;
        store.Transactions[credit.Id] = credit;
        var notification = new Notification(Guid.NewGuid(), sourceAccount.CustomerId, $"Transfer of {request.Amount} sent.", now);
        store.Notifications[notification.Id] = notification;
        return new(true, "Transfer completed.", debit, credit);
    }

    public Loan CreateLoan(CreateLoanRequest request)
    {
        var loan = new Loan(Guid.NewGuid(), request.CustomerId, request.Amount, request.TermMonths, "Pending", DateTime.UtcNow);
        store.Loans[loan.Id] = loan;
        return loan;
    }

    public Statement GetStatement(Guid customerId) => new(customerId, GetAccounts(customerId).ToList(),
        GetAccounts(customerId).SelectMany(a => GetTransactions(a.Id)).OrderByDescending(t => t.CreatedAt).ToList());
}
