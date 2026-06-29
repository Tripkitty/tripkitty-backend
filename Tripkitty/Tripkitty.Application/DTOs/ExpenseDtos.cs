namespace Tripkitty.Application.DTOs;

public record AddExpenseRequest(string Title, decimal Amount, string Payer, List<string> Share);

public record SettlementDto(string From, string To, decimal Amount);

public record SettlementsResponse(Dictionary<string, decimal> Balances, List<SettlementDto> Transactions);
