using Tripkitty.Domain.Entities;

namespace Tripkitty.Application.Logic;

public record Settlement(string From, string To, decimal Amount);

public static class SettlementsCalculator
{
    public static (Dictionary<string, decimal> Balances, Dictionary<string, decimal> OwnBalances, List<Settlement> Transactions) Compute(
        IEnumerable<Expense> expenses)
    {
        // Общий бюджет — по-расходно: у каждого расхода свой снапшот {подопечный → спонсор}
        // (Expense.Sponsors), и доля/платёж подопечного в этом расходе зачисляется спонсору.
        // Один и тот же участник может быть покрыт в одних расходах и платить сам в других.
        // Цепочки внутри расхода невозможны — редирект одношаговый.
        // OwnBalances — персональные балансы до переливаний, для прозрачности на клиенте.
        var bal = new Dictionary<string, decimal>();
        var ownBal = new Dictionary<string, decimal>();

        foreach (var e in expenses)
        {
            var amount = e.AmountMinor / 100m;
            var share = e.Share;
            if (share.Count == 0) continue;

            var sponsors = e.Sponsors;

            void Apply(string participantId, decimal delta)
            {
                ownBal.TryAdd(participantId, 0);
                ownBal[participantId] += delta;

                var effectiveId = sponsors is { Count: > 0 } && sponsors.TryGetValue(participantId, out var sponsor)
                    ? sponsor
                    : participantId;
                bal.TryAdd(effectiveId, 0);
                bal[effectiveId] += delta;
            }

            Apply(e.Payer, amount);

            switch (e.SplitType)
            {
                case SplitType.Equal:
                {
                    var perPerson = Math.Round(amount / share.Count, 2);
                    foreach (var entry in share)
                        Apply(entry.ParticipantId, -perPerson);
                    break;
                }

                case SplitType.ByShares:
                {
                    var totalWeight = share.Sum(s => s.Weight ?? 1);
                    foreach (var entry in share)
                        Apply(entry.ParticipantId, -Math.Round(amount * (entry.Weight ?? 1) / totalWeight, 2));
                    break;
                }

                case SplitType.ByAmounts:
                {
                    foreach (var entry in share)
                        Apply(entry.ParticipantId, -((entry.AmountMinor ?? 0) / 100m));
                    break;
                }
            }
        }

        // Каждый участник расчёта присутствует в Balances, даже если всё покрыто спонсором
        foreach (var id in ownBal.Keys)
            bal.TryAdd(id, 0);

        // Greedy minimization: repeatedly match biggest creditor with biggest debtor
        var tx = new List<Settlement>();

        var creditors = bal
            .Where(x => x.Value > 0.005m)
            .Select(x => (Id: x.Key, Amount: x.Value))
            .OrderByDescending(x => x.Amount)
            .ToList();

        var debtors = bal
            .Where(x => x.Value < -0.005m)
            .Select(x => (Id: x.Key, Amount: x.Value))
            .OrderBy(x => x.Amount)
            .ToList();

        var credIdx = 0;
        var debtIdx = 0;

        var credAmounts = creditors.Select(c => c.Amount).ToList();
        var debtAmounts = debtors.Select(d => d.Amount).ToList();

        while (credIdx < creditors.Count && debtIdx < debtors.Count)
        {
            var cred = creditors[credIdx];
            var debt = debtors[debtIdx];
            var credAmt = credAmounts[credIdx];
            var debtAmt = debtAmounts[debtIdx];

            var settleAmount = Math.Min(credAmt, -debtAmt);

            if (settleAmount > 0.005m)
            {
                tx.Add(new Settlement(debt.Id, cred.Id, Math.Round(settleAmount, 2)));
            }

            credAmounts[credIdx] -= settleAmount;
            debtAmounts[debtIdx] += settleAmount;

            if (Math.Abs(credAmounts[credIdx]) < 0.005m) credIdx++;
            if (Math.Abs(debtAmounts[debtIdx]) < 0.005m) debtIdx++;
        }

        return (bal, ownBal, tx);
    }
}
