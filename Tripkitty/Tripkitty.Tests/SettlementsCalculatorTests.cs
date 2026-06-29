using Tripkitty.Application.Logic;
using Tripkitty.Domain.Entities;

namespace Tripkitty.Tests;

public class SettlementsCalculatorTests
{
    private static Expense MakeExpense(long amountMinor, string payer, List<string> share) =>
        new() { AmountMinor = amountMinor, Payer = payer, Share = share };

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
        // Alice pays 300 RUB, split between Alice and Bob => each owes 150
        // balances stores: payer += amount, each share participant -= perPerson
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
        // Alice pays 30000 minor (300), split [alice, bob] => perPerson=150
        // alice.balance = +300 - 150 = +150, bob.balance = -150
        var expense = MakeExpense(30000, "alice", ["alice", "bob"]);

        var (balances, _) = SettlementsCalculator.Compute([expense]);

        Assert.Equal(300m - 150m, balances["alice"]);
        Assert.Equal(-150m, balances["bob"]);
    }

    [Fact]
    public void SingleExpensePayerNotInShare_PayerGetsFullCredit()
    {
        // Alice pays 30000 for bob and charlie only
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
        // alice pays 6000 split [alice, bob, charlie] => perPerson=20
        // bob pays 3000 split [bob, charlie]          => perPerson=15
        var expenses = new[]
        {
            MakeExpense(6000, "alice", ["alice", "bob", "charlie"]),
            MakeExpense(3000, "bob",   ["bob", "charlie"]),
        };

        var (balances, transactions) = SettlementsCalculator.Compute(expenses);

        // alice: +60 - 20 = +40
        // bob:   +30 - 20 - 15 = -5
        // charlie: -20 - 15 = -35
        Assert.Equal(40m, balances["alice"]);
        Assert.Equal(-5m, balances["bob"]);
        Assert.Equal(-35m, balances["charlie"]);

        // charlie -> alice 35, bob -> alice 5
        var total = transactions.Sum(t => t.Amount);
        Assert.Equal(40m, total);
    }

    [Fact]
    public void BalancedExpenses_NoTransactions()
    {
        // alice pays 10000 for bob, bob pays 10000 for alice => they're even
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
        // alice pays 9000 split equally [alice, bob, charlie] => perPerson=30
        // alice net = +90-30 = +60, bob=-30, charlie=-30
        var expense = MakeExpense(9000, "alice", ["alice", "bob", "charlie"]);

        var (_, transactions) = SettlementsCalculator.Compute([expense]);

        // Greedy produces 2 transactions: bob->alice 30, charlie->alice 30
        Assert.Equal(2, transactions.Count);
        Assert.All(transactions, t => Assert.Equal("alice", t.To));
        Assert.All(transactions, t => Assert.Equal(30m, t.Amount));
    }

    [Fact]
    public void RoundingOddAmount_DoesNotCrash()
    {
        // 100 minor = 1.00, split 3 => 0.33 each
        var expense = MakeExpense(100, "alice", ["alice", "bob", "charlie"]);

        var (balances, transactions) = SettlementsCalculator.Compute([expense]);

        Assert.True(balances.ContainsKey("alice"));
        Assert.All(transactions, t => Assert.True(t.Amount > 0));
    }

    [Fact]
    public void SmallDifferences_BelowEpsilonNotReported()
    {
        // Two expenses that almost cancel each other out — net diff < 0.005
        // alice pays 1 minor for bob, bob pays 1 minor for alice
        var expenses = new[]
        {
            MakeExpense(1, "alice", ["bob"]),
            MakeExpense(1, "bob",   ["alice"]),
        };

        var (_, transactions) = SettlementsCalculator.Compute(expenses);

        Assert.Empty(transactions);
    }
}
