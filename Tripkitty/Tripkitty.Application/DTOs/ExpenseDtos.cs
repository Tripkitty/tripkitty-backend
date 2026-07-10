using Tripkitty.Domain.Entities;

namespace Tripkitty.Application.DTOs;

public record ShareEntryRequest(string ParticipantId, int? Weight = null, decimal? Amount = null);

public record ShareEntryDto(string ParticipantId, int? Weight = null, decimal? Amount = null);

public record AddExpenseRequest(
    string Title,
    decimal Amount,
    string Payer,
    List<ShareEntryRequest> Share,
    SplitType SplitType = SplitType.Equal
);

// Id/IsPaid/PaidAt заполнены только для зафиксированных транзакций (status != active)
public record SettlementDto(
    string From,
    string To,
    decimal Amount,
    PaymentDetailsDto? ToPayment = null,
    string? Id = null,
    bool? IsPaid = null,
    DateTime? PaidAt = null
);

// Balances — после слияния общих бюджетов (у подопечных 0), OwnBalances — персональные до слияния
public record SettlementsResponse(
    string Status,
    Dictionary<string, decimal> Balances,
    Dictionary<string, decimal> OwnBalances,
    List<SettlementDto> Transactions);

public record SetTransactionPaidRequest(bool Paid);
