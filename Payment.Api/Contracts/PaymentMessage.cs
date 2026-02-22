namespace Payment.Api.Contracts
{
    public class PaymentMessage
    {
        public Guid Id { get; set; }
        public decimal Amount { get; set; }
    }
}
