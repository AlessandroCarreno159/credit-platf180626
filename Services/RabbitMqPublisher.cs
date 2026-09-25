using System.Text;
using System.Text.Json;
using RabbitMQ.Client;

namespace credit_platf.Services;

// Productor CloudAMQP (P7). Una conexion + un canal reutilizados (plan Lemming limitado).
// Publica SOLO si la solicitud ya se guardo; con confirms del broker.
// Si falla: la solicitud queda, se loguea y el controller avisa (reenvio manual documentado).
public class RabbitMqPublisher : IAsyncDisposable
{
    private readonly IConfiguration _config;
    private readonly ILogger<RabbitMqPublisher> _log;
    private IConnection? _conn;
    private IChannel? _canal;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public RabbitMqPublisher(IConfiguration config, ILogger<RabbitMqPublisher> log)
    {
        _config = config;
        _log = log;
    }

    public string QueueName => _config["RabbitMq:QueueName"] ?? "solicitudes.notificaciones";

    public async Task<(bool ok, Guid messageId)> PublicarSolicitudRegistradaAsync(int solicitudId, string usuarioId)
        => await PublicarConIdAsync(Guid.NewGuid(), solicitudId, usuarioId);

    // Reenvio manual con el MISMO MessageId (idempotente en el consumidor).
    public async Task<(bool ok, Guid messageId)> PublicarConIdAsync(Guid messageId, int solicitudId, string usuarioId)
    {
        var msg = new SolicitudRegistradaMsg
        {
            MessageId = messageId,
            SolicitudId = solicitudId,
            UsuarioId = usuarioId,
            FechaEventoUtc = DateTime.UtcNow
        };
        try
        {
            var canal = await CanalAsync();
            var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(msg));
            var props = new BasicProperties { Persistent = true, MessageId = messageId.ToString() };
            // Con PublisherConfirmationTrackingEnabled, este await solo termina
            // cuando el broker confirma (ACK) o lanza si hay NACK/retorno.
            await canal.BasicPublishAsync(exchange: string.Empty, routingKey: QueueName,
                mandatory: false, basicProperties: props, body: body);
            _log.LogInformation("MQ publicado+confirmado {MessageId} solicitud {SolicitudId}.", messageId, solicitudId);
            return (true, messageId);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "MQ fallo publicando {MessageId} solicitud {SolicitudId}.", messageId, solicitudId);
            return (false, messageId);
        }
    }

    private async Task<IChannel> CanalAsync()
    {
        if (_canal is { IsOpen: true }) return _canal;
        await _lock.WaitAsync();
        try
        {
            if (_canal is { IsOpen: true }) return _canal;
            _conn?.Dispose();
            var factory = new ConnectionFactory
            {
                Uri = new Uri(_config["RabbitMq:ConnectionString"]
                    ?? throw new InvalidOperationException("Falta RabbitMq:ConnectionString."))
            };
            _conn = await factory.CreateConnectionAsync();
            _canal = await _conn.CreateChannelAsync(new CreateChannelOptions(
                publisherConfirmationsEnabled: true,
                publisherConfirmationTrackingEnabled: true));
            await _canal.QueueDeclareAsync(queue: QueueName, durable: true,
                exclusive: false, autoDelete: false, arguments: null);
            return _canal;
        }
        finally { _lock.Release(); }
    }

    public async ValueTask DisposeAsync()
    {
        if (_canal is not null) await _canal.CloseAsync();
        if (_conn is not null) await _conn.CloseAsync();
        _lock.Dispose();
        GC.SuppressFinalize(this);
    }
}
