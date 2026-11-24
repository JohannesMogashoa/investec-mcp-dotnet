namespace InvestecMcp.Banking;

public interface IInvestecGateway
{
    Task<InvestecResponse<AccountListData>> GetAccountsAsync(CancellationToken cancellationToken);
    Task<InvestecResponse<InvestecBalance>> GetBalanceAsync(string accountId, CancellationToken cancellationToken);
    Task<InvestecResponse<TransactionListData>> GetTransactionsAsync(string accountId, DateOnly fromDate, DateOnly toDate, CancellationToken cancellationToken);
}
