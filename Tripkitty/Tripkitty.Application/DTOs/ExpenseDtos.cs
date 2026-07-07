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

public record SettlementDto(string From, string To, decimal Amount, PaymentDetailsDto? ToPayment = null);

public record SettlementsResponse(Dictionary<string, decimal> Balances, List<SettlementDto> Transactions);
