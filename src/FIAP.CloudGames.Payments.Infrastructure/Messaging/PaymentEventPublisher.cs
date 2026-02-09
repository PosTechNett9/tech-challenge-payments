using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
using FIAP.CloudGames.Payments.Domain.Entities;
using FIAP.CloudGames.Payments.Domain.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Globalization;
using System.Text.Json;

namespace FIAP.CloudGames.Payments.Infrastructure.Messaging
{
    public class PaymentEventPublisher(
        IAmazonSimpleNotificationService sns,
        IConfiguration configuration,
        ILogger<PaymentEventPublisher> logger) : IPaymentEventPublisher
    {
        private readonly IAmazonSimpleNotificationService _sns = sns;
        private readonly ILogger<PaymentEventPublisher> _logger = logger;
        private readonly string _topicArn = configuration["AWS:SNS:PaymentEventsTopicArn"]
                ?? throw new ArgumentNullException("AWS:SNS:PaymentEventsTopicArn not configured");

        public async Task PublishPaymentCreatedAsync(Payment payment)
        {
            try
            {
                var paymentEvent = new
                {
                    paymentId = payment.Id,
                    orderId = payment.OrderId,
                    userId = payment.UserId,
                    amount = payment.Amount,
                    createdAt = payment.CreatedAt
                };

                var messageJson = JsonSerializer.Serialize(paymentEvent);

                var request = new PublishRequest
                {
                    TopicArn = _topicArn,
                    Message = messageJson,
                    MessageAttributes = new Dictionary<string, Amazon.SimpleNotificationService.Model.MessageAttributeValue>
                    {
                        {
                            "MessageType",
                            new Amazon.SimpleNotificationService.Model.MessageAttributeValue
                            {
                                DataType = "String",
                                StringValue = "PaymentCreated"
                            }
                        },
                        {
                            "PaymentId",
                            new Amazon.SimpleNotificationService.Model.MessageAttributeValue
                            {
                                DataType = "String",
                                StringValue = payment.Id.ToString()
                            }
                        },
                        {
                            "Amount",
                            new Amazon.SimpleNotificationService.Model.MessageAttributeValue
                            {
                                DataType = "Number",
                                StringValue = payment.Amount.ToString("F2", CultureInfo.InvariantCulture)
                            }
                        }
                    },
                    Subject = $"Payment Created: {payment.Id}"
                };

                var response = await _sns.PublishAsync(request);

                _logger.LogInformation(
                    "[PAYMENT-EVENT] Published payment created event to SNS: PaymentId={PaymentId}, Amount={Amount}, MessageId={MessageId}",
                    payment.Id,
                    payment.Amount,
                    response.MessageId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "[PAYMENT-EVENT] Error publishing payment created event: PaymentId={PaymentId}",
                    payment.Id);
                throw;
            }
        }
    }
}