namespace MiHotel.Services;

/// <summary>
/// Detecta pausas prolongadas del proceso, como las que ocurren al suspender
/// Windows, e invalida las sesiones creadas antes de dicha pausa.
/// </summary>
public sealed class SuspensionMonitorService : BackgroundService
{
    private static readonly TimeSpan IntervaloRevision = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan PausaDeSuspension = TimeSpan.FromSeconds(30);
    private readonly ILogger<SuspensionMonitorService> _logger;
    private int _generacionActual;

    public SuspensionMonitorService(ILogger<SuspensionMonitorService> logger)
    {
        _logger = logger;
    }

    public int GeneracionActual => Volatile.Read(ref _generacionActual);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        DateTimeOffset ultimaRevision = DateTimeOffset.UtcNow;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(IntervaloRevision, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }

            DateTimeOffset revisionActual = DateTimeOffset.UtcNow;
            TimeSpan pausa = revisionActual - ultimaRevision;
            ultimaRevision = revisionActual;

            if (pausa >= PausaDeSuspension)
            {
                int nuevaGeneracion = Interlocked.Increment(ref _generacionActual);
                _logger.LogInformation(
                    "Se detectó una pausa del equipo de {Segundos:N0} segundos. Las sesiones anteriores a la generación {Generacion} fueron invalidadas.",
                    pausa.TotalSeconds,
                    nuevaGeneracion);
            }
        }
    }
}
