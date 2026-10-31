# Banking Platform

A .NET 8 microservices-ready banking platform foundation with Angular-ready HTTP APIs.

## Services

The API is organized around these bounded contexts: Identity, Customers, Accounts, Transactions, Payments, Loans, Notifications, and Reports. The current development host exposes each context under its own `/api` route group so the workflows are runnable immediately; the boundaries can be deployed as separate processes without changing their contracts.

## Run locally

```powershell
dotnet run --project .\WebApplication1\WebApplication1.csproj
```

Open `/swagger` for the API contract and `/health` for the health check. The Angular client is in `client`.

```powershell
cd .\client
npm install
npm start
```

The API development profile runs at `http://localhost:5179`; the Angular proxy forwards `/api` and `/health` to that address.

## Run infrastructure

```powershell
docker compose up --build
```

SQL Server, RabbitMQ, and Redis are included in the compose topology. The API currently uses an in-memory store so the workflow can be demonstrated without migrations; the configured connection strings are the seam for adding EF Core persistence per service.

## Core workflows

- Register and authenticate a customer
- Create customer accounts
- Transfer money with balance and insufficient-funds validation
- Review account transactions
- Submit loan applications
- Retrieve notifications and customer statements
