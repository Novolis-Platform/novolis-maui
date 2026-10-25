using Novolis.Pdf.Core;

namespace Novolis.Maui.PdfViewer;

/// <summary>Atomically replaces the parsed PDF session after a successful open.</summary>
public sealed class PdfSessionSlot : IAsyncDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private PdfDocumentSession? _current;

    /// <summary>Gets the currently committed session, if any.</summary>
    public PdfDocumentSession? Current => Volatile.Read(ref _current);

    /// <summary>Commits a replacement and disposes the previous session.</summary>
    public async ValueTask<PdfDocumentSession?> ReplaceAsync(PdfDocumentSession next)
    {
        ArgumentNullException.ThrowIfNull(next);
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            var previous = _current;
            _current = next;
            if (previous is not null)
                await previous.DisposeAsync().ConfigureAwait(false);
            return previous;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>Removes and disposes the current session.</summary>
    public async ValueTask ClearAsync()
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            var previous = _current;
            _current = null;
            if (previous is not null)
                await previous.DisposeAsync().ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await ClearAsync().ConfigureAwait(false);
        _gate.Dispose();
    }
}
