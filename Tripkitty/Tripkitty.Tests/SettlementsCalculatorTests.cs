using Tripkitty.Application.Logic;
using Tripkitty.Domain.Entities;

namespace Tripkitty.Tests;

public class SettlementsCalculatorTests
{
    private static Expense MakeExpense(long amountMinor, string payer, List<string> participantIds,
        SplitType splitType = SplitType.Equal, int[]? weights = null, long[]? amounts = null,
        Dictionary<string, string>? sponsors = null) =>
        new()
        {
            AmountMinor = amountMinor,
            Payer = payer,
            SplitType = splitType,
            Share = participantIds.Select((id, i) => new ShareEntry
            {
                ParticipantId = id,
                Weight = weights?[i],
                AmountMinor = amounts?[i]
            }).ToList(),
            Sponsors = sponsors ?? new Dictionary<string, string>()
        };

    [Fact]
    public void NoExpenses_ReturnsEmptyBalancesAndTransactions()
    {
        var (balances, _, transactions) = SettlementsCalculator.Compute([]);

        Assert.Empty(balances);
        Assert.Empty(transactions);
    }

    [Fact]
    public void SingleExpenseEqualSplit_BalancesCorrect()
    {
        // Alice pays 300, split [alice, bob] => each owes 150
        // alice: +300 - 150 = +150, bob: -150
        var expense = MakeExpense(30000, "alice", ["alice", "bob"]);

        var (balances, _, transactions) = SettlementsCalculator.Compute([expense]);

        Assert.Equal(150m, balances["alice"]);
        Assert.Equal(-150m, balances["bob"]);
        Assert.Single(transactions);
        Assert.Equal("bob", transactions[0].From);
        Assert.Equal("alice", transactions[0].To);
        Assert.Equal(150m, transactions[0].Amount);
    }

    [Fact]
    public void SingleExpensePayerInShare_NetBalanceIsCorrect()
    {
        var expense = MakeExpense(30000, "alice", ["alice", "bob"]);

        var (balances, _, _) = SettlementsCalculator.Compute([expense]);

        Assert.Equal(300m - 150m, balances["alice"]);
        Assert.Equal(-150m, balances["bob"]);
    }

    [Fact]
    public void SingleExpensePayerNotInShare_PayerGetsFullCredit()
    {
        var expense = MakeExpense(30000, "alice", ["bob", "charlie"]);

        var (balances, _, transactions) = SettlementsCalculator.Compute([expense]);

        Assert.Equal(300m, balances["alice"]);
        Assert.Equal(-150m, balances["bob"]);
        Assert.Equal(-150m, balances["charlie"]);
        Assert.Equal(2, transactions.Count);
        Assert.All(transactions, t => Assert.Equal("alice", t.To));
    }

    [Fact]
    public void ExpenseWithEmptyShare_IsSkipped()
    {
        var expense = MakeExpense(10000, "alice", []);

        var (balances, _, transactions) = SettlementsCalculator.Compute([expense]);

        Assert.Empty(balances);
        Assert.Empty(transactions);
    }

    [Fact]
    public void ThreePeople_MultipleExpenses_CorrectTransactions()
    {
        var expenses = new[]
        {
            MakeExpense(6000, "alice", ["alice", "bob", "charlie"]),
            MakeExpense(3000, "bob",   ["bob", "charlie"]),
        };

        var (balances, _, transactions) = SettlementsCalculator.Compute(expenses);

        Assert.Equal(40m, balances["alice"]);
        Assert.Equal(-5m, balances["bob"]);
        Assert.Equal(-35m, balances["charlie"]);

        var total = transactions.Sum(t => t.Amount);
        Assert.Equal(40m, total);
    }

    [Fact]
    public void BalancedExpenses_NoTransactions()
    {
        var expenses = new[]
        {
            MakeExpense(10000, "alice", ["bob"]),
            MakeExpense(10000, "bob",   ["alice"]),
        };

        var (_, _, transactions) = SettlementsCalculator.Compute(expenses);

        Assert.Empty(transactions);
    }

    [Fact]
    public void ThreeWaySplit_MinimizesTransactionCount()
    {
        var expense = MakeExpense(9000, "alice", ["alice", "bob", "charlie"]);

        var (_, _, transactions) = SettlementsCalculator.Compute([expense]);

        Assert.Equal(2, transactions.Count);
        Assert.All(transactions, t => Assert.Equal("alice", t.To));
        Assert.All(transactions, t => Assert.Equal(30m, t.Amount));
    }

    [Fact]
    public void RoundingOddAmount_DoesNotCrash()
    {
        var expense = MakeExpense(100, "alice", ["alice", "bob", "charlie"]);

        var (balances, _, transactions) = SettlementsCalculator.Compute([expense]);

        Assert.True(balances.ContainsKey("alice"));
        Assert.All(transactions, t => Assert.True(t.Amount > 0));
    }

    [Fact]
    public void SmallDifferences_BelowEpsilonNotReported()
    {
        var expenses = new[]
        {
            MakeExpense(1, "alice", ["bob"]),
            MakeExpense(1, "bob",   ["alice"]),
        };

        var (_, _, transactions) = SettlementsCalculator.Compute(expenses);

        Assert.Empty(transactions);
    }

    [Fact]
    public void ByShares_TwoPeople_WeightedSplit()
    {
        // Alice pays 9000 minor (90), ratio 2:1 => alice owes 60, bob owes 30
        // alice: +90 - 60 = +30, bob: -30
        var expense = MakeExpense(9000, "alice", ["alice", "bob"],
            SplitType.ByShares, weights: [2, 1]);

        var (balances, _, transactions) = SettlementsCalculator.Compute([expense]);

        Assert.Equal(30m, balances["alice"]);
        Assert.Equal(-30m, balances["bob"]);
        Assert.Single(transactions);
        Assert.Equal("bob", transactions[0].From);
        Assert.Equal("alice", transactions[0].To);
        Assert.Equal(30m, transactions[0].Amount);
    }

    [Fact]
    public void ByShares_PayerInShare_CorrectNetBalance()
    {
        // Bob pays 12000 (120), shares alice:1, bob:3 => total 4 parts, 30/part
        // alice owes 30, bob owes 90
        // bob: +120 - 90 = +30, alice: -30
        var expense = MakeExpense(12000, "bob", ["alice", "bob"],
            SplitType.ByShares, weights: [1, 3]);

        var (balances, _, _) = SettlementsCalculator.Compute([expense]);

        Assert.Equal(-30m, balances["alice"]);
        Assert.Equal(30m, balances["bob"]);
    }

    [Fact]
    public void ByAmounts_ThreePeople_CustomAmounts()
    {
        // Alice pays 10000 (100): alice 5000 (50), bob 3000 (30), charlie 2000 (20)
        // alice: +100 - 50 = +50, bob: -30, charlie: -20
        var expense = MakeExpense(10000, "alice", ["alice", "bob", "charlie"],
            SplitType.ByAmounts, amounts: [5000, 3000, 2000]);

        var (balances, _, transactions) = SettlementsCalculator.Compute([expense]);

        Assert.Equal(50m, balances["alice"]);
        Assert.Equal(-30m, balances["bob"]);
        Assert.Equal(-20m, balances["charlie"]);
        Assert.Equal(2, transactions.Count);
        Assert.All(transactions, t => Assert.Equal("alice", t.To));
    }

    [Fact]
    public void ByAmounts_PayerNotInShare_FullCredit()
    {
        // Alice pays 10000 (100) for bob 6000 (60) and charlie 4000 (40)
        // alice: +100, bob: -60, charlie: -40
        var expense = MakeExpense(10000, "alice", ["bob", "charlie"],
            SplitType.ByAmounts, amounts: [6000, 4000]);

        var (balances, _, transactions) = SettlementsCalculator.Compute([expense]);

        Assert.Equal(100m, balances["alice"]);
        Assert.Equal(-60m, balances["bob"]);
        Assert.Equal(-40m, balances["charlie"]);
        Assert.Equal(2, transactions.Count);
    }

    [Fact]
    public void Sponsor_AbsorbsDependentDebt_DependentNotInTransactions()
    {
        // Bob pays 300 for [alice, wife, bob]; alice sponsors wife
        // own: bob +200, alice -100, wife -100 => merged: alice -200
        var expense = MakeExpense(30000, "bob", ["alice", "wife", "bob"],
            sponsors: new() { ["wife"] = "alice" });

        var (balances, ownBalances, transactions) = SettlementsCalculator.Compute([expense]);

        Assert.Equal(-200m, balances["alice"]);
        Assert.Equal(0m, balances["wife"]);
        Assert.Equal(-100m, ownBalances["alice"]);
        Assert.Equal(-100m, ownBalances["wife"]);

        var tx = Assert.Single(transactions);
        Assert.Equal("alice", tx.From);
        Assert.Equal("bob", tx.To);
        Assert.Equal(200m, tx.Amount);
    }

    [Fact]
    public void Sponsor_DependentPayment_CreditsSponsor()
    {
        // Wife pays 300 for [alice, wife, bob]; alice sponsors wife
        // own: wife +200, alice -100, bob -100 => merged: alice +100, bob -100
        var expense = MakeExpense(30000, "wife", ["alice", "wife", "bob"],
            sponsors: new() { ["wife"] = "alice" });

        var (balances, _, transactions) = SettlementsCalculator.Compute([expense]);

        Assert.Equal(100m, balances["alice"]);
        Assert.Equal(0m, balances["wife"]);
        Assert.Equal(-100m, balances["bob"]);

        var tx = Assert.Single(transactions);
        Assert.Equal("bob", tx.From);
        Assert.Equal("alice", tx.To);
        Assert.Equal(100m, tx.Amount);
    }

    [Fact]
    public void Sponsor_GroupInternalExpense_SettlesToZero()
    {
        // Alice pays 200 for [alice, wife]; alice sponsors wife — внутри бюджета долгов нет
        var expense = MakeExpense(20000, "alice", ["alice", "wife"],
            sponsors: new() { ["wife"] = "alice" });

        var (balances, _, transactions) = SettlementsCalculator.Compute([expense]);

        Assert.Equal(0m, balances["alice"]);
        Assert.Equal(0m, balances["wife"]);
        Assert.Empty(transactions);
    }

    [Fact]
    public void Sponsor_MultipleDependents_AllMergeIntoSponsor()
    {
        // Bob pays 400 for [alice, bro1, bro2, bob]; alice sponsors both brothers
        var expense = MakeExpense(40000, "bob", ["alice", "bro1", "bro2", "bob"],
            sponsors: new() { ["bro1"] = "alice", ["bro2"] = "alice" });

        var (balances, _, transactions) = SettlementsCalculator.Compute([expense]);

        Assert.Equal(-300m, balances["alice"]);
        Assert.Equal(0m, balances["bro1"]);
        Assert.Equal(0m, balances["bro2"]);

        var tx = Assert.Single(transactions);
        Assert.Equal("alice", tx.From);
        Assert.Equal("bob", tx.To);
        Assert.Equal(300m, tx.Amount);
    }

    [Fact]
    public void NoSponsors_OwnBalancesEqualBalances()
    {
        var expense = MakeExpense(30000, "alice", ["alice", "bob"]);

        var (balances, ownBalances, _) = SettlementsCalculator.Compute([expense]);

        Assert.Equal(balances, ownBalances);
    }

    [Fact]
    public void PerExpenseSponsors_MixedExpenses_OnlySponsoredOnesRedirect()
    {
        // Ресторан: alice платит 300 за [alice, wife, bob], долю wife покрывает alice.
        // Шопинг: bob платит 100 за [wife] — без спонсорства, чисто её трата.
        var restaurant = MakeExpense(30000, "alice", ["alice", "wife", "bob"],
            sponsors: new() { ["wife"] = "alice" });
        var shopping = MakeExpense(10000, "bob", ["wife"]);

        var (balances, ownBalances, transactions) = SettlementsCalculator.Compute([restaurant, shopping]);

        // Ресторан: alice +300-100-100(за wife) = +100, bob -100.
        // Шопинг: wife -100, bob +100 => итог: alice +100, bob 0, wife -100
        Assert.Equal(100m, balances["alice"]);
        Assert.Equal(0m, balances["bob"]);
        Assert.Equal(-100m, balances["wife"]);

        Assert.Equal(-200m, ownBalances["wife"]); // персонально: и ресторан, и шопинг

        var tx = Assert.Single(transactions);
        Assert.Equal("wife", tx.From);
        Assert.Equal("alice", tx.To);
        Assert.Equal(100m, tx.Amount);
    }

    [Fact]
    public void PerExpenseSponsors_FullyCoveredDependent_PresentInBalancesWithZero()
    {
        var expense = MakeExpense(30000, "bob", ["alice", "wife", "bob"],
            sponsors: new() { ["wife"] = "alice" });

        var (balances, _, _) = SettlementsCalculator.Compute([expense]);

        // Подопечный не выпадает из Balances, даже если всё покрыто спонсором
        Assert.True(balances.ContainsKey("wife"));
        Assert.Equal(0m, balances["wife"]);
    }

    [Fact]
    public void PerExpenseSponsors_SponsorNotElsewhereInvolved_ReceivesRedirect()
    {
        // Спонсор вообще не участвует в расходе — но долю подопечного получает он
        var expense = MakeExpense(20000, "bob", ["wife", "bob"],
            sponsors: new() { ["wife"] = "alice" });

        var (balances, _, transactions) = SettlementsCalculator.Compute([expense]);

        Assert.Equal(-100m, balances["alice"]);
        Assert.Equal(0m, balances["wife"]);

        var tx = Assert.Single(transactions);
        Assert.Equal("alice", tx.From);
        Assert.Equal("bob", tx.To);
    }
}
