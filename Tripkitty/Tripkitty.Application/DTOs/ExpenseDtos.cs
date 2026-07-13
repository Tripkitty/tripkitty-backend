using Tripkitty.Domain.Entities;

namespace Tripkitty.Application.DTOs;

public record ShareEntryRequest(string ParticipantId, int? Weight = null, decimal? Amount = null);

public record ShareEntryDto(string ParticipantId, int? Weight = null, decimal? Amount = null);

// Sponsors: {подопечный → спонсор} для этого расхода. null = не трогать
// (при создании — снапшот текущего общего бюджета поездки, при правке — оставить как было);
// {} = никто никого не покрывает. Разрешены только живые пары спонсорства
// плюс пары, уже записанные на этом расходе.
public record AddExpenseRequest(
    string Title,
    decimal Amount,
    string Payer,
    List<ShareEntryRequest> Share,
    SplitType SplitType = SplitType.Equal,
    decimal? GrossAmount = null,
    decimal? DiscountPercent = null,
    decimal? DiscountAmount = null,
    Dictionary<string, string>? Sponsors = null
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

// Balances — с учётом общих бюджетов (покрытые спонсором доли зачислены спонсору),
// OwnBalances — персональные балансы до переливаний
public record SettlementsResponse(
    string Status,
    Dictionary<string, decimal> Balances,
    Dictionary<string, decimal> OwnBalances,
    List<SettlementDto> Transactions);

public record SetTransactionPaidRequest(bool Paid);
