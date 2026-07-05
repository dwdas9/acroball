using System.Text;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;

namespace Acroball.Infrastructure.Logging;

/// <summary>
/// A deliberately small rolling file logger: one file per day
/// (<c>Acroball-yyyyMMdd.log</c>), written by a single background consumer fed
/// through a channel so logging never blocks callers.
/// </summary>
/// <remarks>
/// Chosen over Serilog to keep the dependency surface minimal for what this
/// app needs â€” plain diagnostic text files (see ADR-0006). Level filtering is
/// left to <see cref="ILoggerFactory"/> configuration; this provider writes
/// whatever it is handed. Files older than <see cref="RetentionDays"/> are
/// deleted at startup.
/// </remarks>
public sealed class FileLoggerProvider : ILoggerProvider
{
    /// <summary>Log files older than this many days are deleted at startup.</summary>
    public const int RetentionDays = 14;

    private readonly string _directory;
    private readonly Channel<string> _channel;
    private readonly Task _writerTask;
    private readonly CancellationTokenSource _shutdown = new();

    /// <summary>Creates the provider writing into <paramref name="logDirectory"/>.</summary>
    public FileLoggerProvider(string logDirectory)
    {
        _directory = logDirectory;
        Directory.CreateDirectory(_directory);
        DeleteExpiredFiles();

        _channel = Channel.CreateUnbounded<string>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false,
        });

        _writerTask = Task.Run(WriteLoopAsync);
    }

    /// <inheritdoc />
    public ILogger CreateLogger(string categoryName) => new FileLogger(categoryName, _channel.Writer);

    /// <inheritdoc />
    public void Dispose()
    {
        _channel.Writer.TryComplete();

        // Give the writer a moment to drain, then let go; losing the final
        // few lines on a hard shutdown is acceptable for a diagnostic log.
        try
        {
            _writerTask.Wait(TimeSpan.FromSeconds(2));
        }
        catch (AggregateException)
        {
            // Swallow: shutdown path must never throw.
        }

        _shutdown.Cancel();
        _shutdown.Dispose();
    }

    private async Task WriteLoopAsync()
    {
        var reader = _channel.Reader;

        while (await reader.WaitToReadAsync().ConfigureAwait(false))
        {
            var path = Path.Combine(_directory, $"Acroball-{DateTime.Now:yyyyMMdd}.log");

            try
            {
                await using var writer = new StreamWriter(path, append: true, Encoding.UTF8);
                while (reader.TryRead(out var line))
                {
                    await writer.WriteLineAsync(line).ConfigureAwait(false);
                }

                await writer.FlushAsync().ConfigureAwait(false);
            }
            catch (IOException)
            {
                // Disk hiccups must not take the app down; drop the batch.
            }
        }
    }

    private void DeleteExpiredFiles()
    {
        try
        {
            var cutoff = DateTime.Now.AddDays(-RetentionDays);
            foreach (var file in Directory.EnumerateFiles(_directory, "Acroball-*.log"))
            {
                if (File.GetLastWriteTime(file) < cutoff)
                {
                    File.Delete(file);
                }
            }
        }
        catch (Exception)
        {
            // Retention is best-effort.
        }
    }

    private sealed class FileLogger : ILogger
    {
        private readonly string _category;
        private readonly ChannelWriter<string> _writer;

        public FileLogger(string category, ChannelWriter<string> writer)
        {
            _category = category;
            _writer = writer;
        }

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
            => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
            {
                return;
            }

            var builder = new StringBuilder(160)
                .Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"))
                .Append(" [").Append(Abbreviate(logLevel)).Append("] ")
                .Append(_category)
                .Append(": ")
                .Append(formatter(state, exception));

            if (exception is not null)
            {
                builder.AppendLine().Append(exception);
            }

            _writer.TryWrite(builder.ToString());
        }

        private static string Abbreviate(LogLevel level) => level switch
        {
            LogLevel.Trace => "TRC",
            LogLevel.Debug => "DBG",
            LogLevel.Information => "INF",
            LogLevel.Warning => "WRN",
            LogLevel.Error => "ERR",
            LogLevel.Critical => "CRT",
            _ => "???",
        };
    }
}

