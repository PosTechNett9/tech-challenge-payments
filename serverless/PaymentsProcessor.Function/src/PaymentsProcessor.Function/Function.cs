using Amazon.Lambda.Core;
using Amazon.Lambda.SQSEvents;
using System.Text.Json;
using Microsoft.Data.SqlClient;

[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace PaymentsProcessor.Function;

public class Function
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public Function()
    {
    }

    public async Task FunctionHandler(SQSEvent evnt, ILambdaContext context)
    {
        foreach (var message in evnt.Records)
        {
            await ProcessMessageAsync(message, context);
        }
    }

    private async Task ProcessMessageAsync(SQSEvent.SQSMessage message, ILambdaContext context)
    {
        try
        {
            context.Logger.LogInformation($"[PAYMENT] Processing SQS message: {message.MessageId}");

            SnsMessageWrapper? snsWrapper = null;
            PaymentCreatedEvent? paymentEvent = null;

            try
            {
                snsWrapper = JsonSerializer.Deserialize<SnsMessageWrapper>(message.Body, JsonOptions);

                if (snsWrapper != null && !string.IsNullOrEmpty(snsWrapper.Message))
                {
                    paymentEvent = JsonSerializer.Deserialize<PaymentCreatedEvent>(snsWrapper.Message, JsonOptions);
                    context.Logger.LogInformation($"[PAYMENT] Extracted payment event from SNS wrapper");
                }
                else
                {
                    paymentEvent = JsonSerializer.Deserialize<PaymentCreatedEvent>(message.Body, JsonOptions);
                    context.Logger.LogInformation($"[PAYMENT] Deserialized payment event directly from body");
                }
            }
            catch (Exception ex)
            {
                context.Logger.LogError($"[PAYMENT] Error deserializing message: {ex.Message}");
                throw;
            }

            if (paymentEvent == null)
            {
                context.Logger.LogWarning($"[PAYMENT] Failed to deserialize PaymentCreatedEvent");
                return;
            }

            context.Logger.LogInformation(
                $"[PAYMENT] Processing payment {paymentEvent.PaymentId} - Amount: {paymentEvent.Amount}");

            // Simula processamento (gateway, antifraude, etc.)

            context.Logger.LogInformation($"[PAYMENT] Simulating payment gateway processing...");
            await Task.Delay(5000);

            var connStr = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");
            if (string.IsNullOrWhiteSpace(connStr))
            {
                throw new Exception("ConnectionStrings__DefaultConnection não configurada");
            }

            await using var conn = new SqlConnection(connStr);
            await conn.OpenAsync();

            await using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                UPDATE Payments
                SET Status = @status
                WHERE Id = @id";

            cmd.Parameters.AddWithValue("@status", 2);
            cmd.Parameters.AddWithValue("@id", paymentEvent.PaymentId);

            var rows = await cmd.ExecuteNonQueryAsync();

            context.Logger.LogInformation($"[PAYMENT] UPDATE rows affected: {rows}");

            if (rows == 0)
            {
                context.Logger.LogWarning(
                    $"[PAYMENT] Nenhum registro encontrado para Id={paymentEvent.PaymentId}. " +
                    $"Verifique se o payment foi criado nesse mesmo DB.");
            }
            else
            {
                context.Logger.LogInformation(
                    $"[PAYMENT] Payment {paymentEvent.PaymentId} processed successfully and approved");
            }
        }
        catch (Exception ex)
        {
            context.Logger.LogError($"[PAYMENT] Error processing message: {ex.Message}");
            context.Logger.LogError($"[PAYMENT] Stack trace: {ex.StackTrace}");
            throw;
        }
    }
}

public class SnsMessageWrapper
{
    public string Message { get; set; } = string.Empty;
    public string MessageId { get; set; } = string.Empty;
    public string Timestamp { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
}