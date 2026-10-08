namespace ProductionFlow.Api.Domain;

public enum ProductionOrderStatus
{
    Draft = 0,
    Released = 1,
    InProgress = 2,
    Completed = 3,
    Cancelled = 4
}