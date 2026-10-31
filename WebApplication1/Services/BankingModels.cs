namespace BankingPlatform.Api.Services;

public record LoginRequest(string Email, string Password);
public record RegisterRequest(string FullName, string Email, string Password);
public record CreateCustomerRequest(string FullName, string Email, string Phone)
{
    public Guid Id { get; init; } = Guid.NewGuid();
}
public record CreateAccountRequest(Guid CustomerId, string Type, string Currency);
public record DepositRequest(decimal Amount);
public record TransferRequest(Guid FromAccountId, Guid ToAccountId, decimal Amount, string? Description);
public record PaymentRequest(Guid FromAccountId, Guid ToAccountId, decimal Amount, string? Description);
public record CreateLoanRequest(Guid CustomerId, decimal Amount, int TermMonths);

public record Customer(Guid Id, string FullName, string Email, string Phone, DateTime CreatedAt);
public record Account(Guid Id, Guid CustomerId, string Number, string Type, string Currency, decimal Balance, string Status);
public record Transaction(Guid Id, Guid AccountId, decimal Amount, string Type, string Description, DateTime CreatedAt);
public record Loan(Guid Id, Guid CustomerId, decimal Principal, int TermMonths, string Status, DateTime CreatedAt);
public record Notification(Guid Id, Guid CustomerId, string Message, DateTime CreatedAt);
public record Statement(Guid CustomerId, IReadOnlyList<Account> Accounts, IReadOnlyList<Transaction> Transactions);
public record LoginResponse(string Token, Customer Customer);
public record TransferResult(bool Success, string Message, Transaction? Debit, Transaction? Credit);
