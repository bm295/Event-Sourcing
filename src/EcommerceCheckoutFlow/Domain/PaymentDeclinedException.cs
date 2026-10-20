namespace EcommerceCheckoutFlow.Domain;

// Only a definitive business rejection may produce PaymentFailed. Timeouts and
// transport errors have an unknown result and must be retried with the same key.
public sealed class PaymentDeclinedException(string message) : Exception(message);
