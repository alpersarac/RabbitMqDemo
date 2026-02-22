using Microsoft.AspNetCore.Mvc;
using Payment.Api.Contracts;
using Payment.Api.Messaging;

namespace Payment.Api.Controllers
{
    [ApiController]
    [Route("payments")]
    public class PaymentsController : Controller
    {
       private readonly RabbitPublisher _publisher;
        public PaymentsController(RabbitPublisher publisher)
        {
            _publisher = publisher;
        }

        [HttpPost]
        public IActionResult Create(decimal amount)
        {
            var message = new PaymentMessage
            {
                Id=Guid.NewGuid(),
                Amount = amount,
            };
            _publisher.Publish(message);

            return Accepted(message.Id);
        }
    }
}
