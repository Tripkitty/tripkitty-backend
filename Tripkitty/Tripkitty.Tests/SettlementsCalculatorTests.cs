using Tripkitty.Application.Logic;
using Tripkitty.Domain.Entities;

namespace Tripkitty.Tests;

public class SettlementsCalculatorTests
{
    private static Expense MakeExpense(long amountMinor, string payer, List<string> participantIds,
        SplitType splitType = SplitType.Equal, int[]? weights = null, long[]? amounts = null) =>
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
            }).ToList()
        };

    [Fact]
    public void NoExpenses_ReturnsEmptyBalancesAndTransactions()
    {
        var (balances, transactions) = SettlementsCalculator.Compute([]);

        Assert.Empty(balances);
        Assert.Empty(transactions);
    }

    [Fact]
    public void SingleExpenseEqualSplit_BalancesCorrect()
    {
        // Alice pays 300, split [alice, bob] => each owes 150
        // alice: +300 - 150 = +150, bob: -150
        var expense = MakeExpense(30000, "alice", ["alice", "bob"]);

        var (balances, transactions) = SettlementsCalculator.Compute([expense]);

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

        var (balances, _) = SettlementsCalculator.Compute([expense]);

        Assert.Equal(300m - 150m, balances["alice"]);
        Assert.Equal(-150m, balances["bob"]);
    }

    [Fact]
    public void SingleExpensePayerNotInShare_PayerGetsFullCredit()
    {
        var expense = MakeExpense(30000, "alice", ["bob", "charlie"]);

        var (balances, transactions) = SettlementsCalculator.Compute([expense]);

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

        var (balances, transactions) = SettlementsCalculator.Compute([expense]);

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

        var (balances, transactions) = SettlementsCalculator.Compute(expenses);

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

        var (_, transactions) = SettlementsCalculator.Compute(expenses);

        Assert.Empty(transactions);
    }

    [Fact]
    public void ThreeWaySplit_MinimizesTransactionCount()
    {
        var expense = MakeExpense(9000, "alice", ["alice", "bob", "charlie"]);

        var (_, transactions) = SettlementsCalculator.Compute([expense]);

        Assert.Equal(2, transactions.Count);
        Assert.All(transactions, t => Assert.Equal("alice", t.To));
        Assert.All(transactions, t => Assert.Equal(30m, t.Amount));
    }

    [Fact]
    public void RoundingOddAmount_DoesNotCrash()
    {
        var expense = MakeExpense(100, "alice", ["alice", "bob", "charlie"]);

        var (balances, transactions) = SettlementsCalculator.Compute([expense]);

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

        var (_, transactions) = SettlementsCalculator.Compute(expenses);

        Assert.Empty(transactions);
    }

    [Fact]
    public void ByShares_TwoPeople_WeightedSplit()
    {
        // Alice pays 9000 minor (90), ratio 2:1 => alice owes 60, bob owes 30
        // alice: +90 - 60 = +30, bob: -30
        var expense = MakeExpense(9000, "alice", ["alice", "bob"],
            SplitType.ByShares, weights: [2, 1]);

        var (balances, transactions) = SettlementsCalculator.Compute([expense]);

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

        var (balances, _) = SettlementsCalculator.Compute([expense]);

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

        var (balances, transactions) = SettlementsCalculator.Compute([expense]);

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

        var (balances, transactions) = SettlementsCalculator.Compute([expense]);

        Assert.Equal(100m, balances["alice"]);
        Assert.Equal(-60m, balances["bob"]);
        Assert.Equal(-40m, balances["charlie"]);
        Assert.Equal(2, transactions.Count);
    }
}
