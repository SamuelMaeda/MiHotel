using System.Collections.Concurrent;

namespace MiHotel.Infrastructure;

public sealed class ArchivoLoggerProvider : ILoggerProvider
{
    private readonly string _directorio;
    private readonly int _diasRetencion;
    private readonly ConcurrentDictionary<string, ArchivoLogger> _loggers = new();
    private readonly object _bloqueo = new();

    public ArchivoLoggerProvider(string directorio, int diasRetencion = 15)
    {
        _directorio = directorio;
        _diasRetencion = Math.Max(1, diasRetencion);
        Directory.CreateDirectory(_directorio);
        EliminarRegistrosAntiguos();
    }

    public ILogger CreateLogger(string categoryName) =>
        _loggers.GetOrAdd(categoryName, categoria => new ArchivoLogger(categoria, Escribir));

    private void Escribir(string linea)
    {
        lock (_bloqueo)
        {
            string archivo = Path.Combine(_directorio, $"mihotel-{DateTime.Now:yyyy-MM-dd}.log");
            File.AppendAllText(archivo, linea + Environment.NewLine);
        }
    }

    private void EliminarRegistrosAntiguos()
    {
        DateTime limite = DateTime.Now.Date.AddDays(-_diasRetencion);
        foreach (string archivo in Directory.EnumerateFiles(_directorio, "mihotel-*.log"))
        {
            try
            {
                if (File.GetLastWriteTime(archivo) < limite) File.Delete(archivo);
            }
            catch
            {
                // Un archivo en uso se intentará limpiar en un inicio posterior.
            }
        }
    }

    public void Dispose() => _loggers.Clear();

    private sealed class ArchivoLogger : ILogger
    {
        private readonly string _categoria;
        private readonly Action<string> _escribir;

        public ArchivoLogger(string categoria, Action<string> escribir)
        {
            _categoria = categoria;
            _escribir = escribir;
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Information;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
            Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel)) return;
            string mensaje = formatter(state, exception);
            string detalle = exception == null ? "" : $" | {exception}";
            _escribir($"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{logLevel}] {_categoria}: {mensaje}{detalle}");
        }
    }
}
