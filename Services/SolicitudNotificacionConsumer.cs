using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using credit_platf.Data;
using credit_platf.Models;

namespace credit_platf.Services;

// Consumidor CloudAMQP (P7). ACK manual SOLO despues de guardar la Notificacion.
// MessageId unico => una redelivery no duplica. Invalido => Nack sin reencolar + log.
// Error de proceso => Nack sin reencolar + log (reenvio manual documentado, sin reintentos infinitos).
// No arranca si RabbitMq:ConsumerEnabled=false (demo de mensaje pendiente en el broker).
public class SolicitudNotificacionConsumer(
    IConfiguration config,
    IServiceScopeFactory scopes,
    ILogger<SolicitudNotificacionConsumer> log) : BackgroundService
{
    public const string TextoRecepcion = "Recibimos tu solicitud de crédito y está pendiente de evaluación.";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!config.GetValue<bool>("RabbitMq:ConsumerEnabled"))
        {
            log.LogWarning("MQ consumidor desactivado (RabbitMq:ConsumerEnabled=false).");
            return;
        }
        var queue = config["RabbitMq:QueueName"] ?? "solicitudes.notificaciones";
        var factory = new ConnectionFactory
        {
            Uri = new Uri(config["RabbitMq:ConnectionString"]
                ?? throw new InvalidOperationException("Falta RabbitMq:ConnectionString."))
        };
        var conn = await factory.CreateConnectionAsync(stoppingToken);
        var canal = await conn.CreateChannelAsync(cancellationToken: stoppingToken);
        await canal.QueueDeclareAsync(queue: queue, durable: true,
            exclusive: false, autoDelete: false, arguments: null);
        await canal.BasicQosAsync(prefetchSize: 0, prefetchCount: 1, global: false,
            cancellationToken: stoppingToken);

        var consumer = new AsyncEventingBasicConsumer(canal);
        consumer.ReceivedAsync += (sender, args) => ProcesarAsync(canal, args, stoppingToken);
        await canal.BasicConsumeAsync(queue: queue, autoAck: false, consumer: consumer,
            cancellationToken: stoppingToken);
        log.LogInformation("MQ consumidor escuchando {Queue}.", queue);

        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    private async Task ProcesarAsync(IChannel canal, BasicDeliverEventArgs args, CancellationToken ct)
    {
        SolicitudRegistradaMsg? msg = null;
        try
        {
            msg = JsonSerializer.Deserialize<SolicitudRegistradaMsg>(Encoding.UTF8.GetString(args.Body.ToArray()));
        }
        catch (Exception ex)
        {
            log.LogError(ex, "MQ mensaje invalido; se rechaza sin reencolar.");
            await canal.BasicNackAsync(args.DeliveryTag, multiple: false, requeue: false, cancellationToken: ct);
            return;
        }
        if (msg is null || msg.MessageId == Guid.Empty || msg.SolicitudId <= 0 || string.IsNullOrWhiteSpace(msg.UsuarioId))
        {
            log.LogWarning("MQ mensaje incompleto; se rechaza sin reencolar.");
            await canal.BasicNackAsync(args.DeliveryTag, multiple: false, requeue: false, cancellationToken: ct);
            return;
        }

        try
        {
            using var scope = scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            if (!await db.Notificaciones.AnyAsync(n => n.MessageId == msg.MessageId, ct))
            {
                db.Notificaciones.Add(new Notificacion
                {
                    MessageId = msg.MessageId,
                    SolicitudId = msg.SolicitudId,
                    UsuarioId = msg.UsuarioId,
                    Texto = TextoRecepcion,
                    FechaProcesamientoUtc = DateTime.UtcNow
                });
                await db.SaveChangesAsync(ct);
                log.LogInformation("MQ notificacion guardada {MessageId}.", msg.MessageId);
            }
            else
            {
                log.LogInformation("MQ duplicado {MessageId}: ACK sin insertar.", msg.MessageId);
            }
            await canal.BasicAckAsync(args.DeliveryTag, multiple: false, cancellationToken: ct);
        }
        catch (Exception ex)
        {
            log.LogError(ex, "MQ fallo procesando {MessageId}; Nack sin reencolar (reenvio manual).", msg.MessageId);
            await canal.BasicNackAsync(args.DeliveryTag, multiple: false, requeue: false, cancellationToken: ct);
        }
    }
}
