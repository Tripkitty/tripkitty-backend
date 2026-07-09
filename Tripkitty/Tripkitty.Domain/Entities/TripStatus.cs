namespace Tripkitty.Domain.Entities;

public enum TripStatus
{
    Active = 0,   // расходы накидываются, settlements — предварительный расчёт
    Settling = 1, // подсчёт финализирован, транзакции зафиксированы, идут переводы
    Settled = 2   // все транзакции отмечены оплаченными
}
