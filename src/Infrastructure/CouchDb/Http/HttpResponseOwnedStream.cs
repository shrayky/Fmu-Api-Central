namespace CouchDb.Http;

/// <summary>
/// Поток тела HTTP-ответа, который закрывает ответ вместе с собой.
/// </summary>
internal sealed class HttpResponseOwnedStream : Stream
{
    private readonly HttpResponseMessage _response;
    private readonly Stream _inner;

    public HttpResponseOwnedStream(HttpResponseMessage response, Stream inner)
    {
        _response = response;
        _inner = inner;
    }

    public override bool CanRead => _inner.CanRead;
    public override bool CanSeek => _inner.CanSeek;
    public override bool CanWrite => false;
    public override long Length => _inner.CanSeek ? _inner.Length : throw new NotSupportedException();
    public override long Position
    {
        get => _inner.CanSeek ? _inner.Position : throw new NotSupportedException();
        set
        {
            if (!_inner.CanSeek)
                throw new NotSupportedException();

            _inner.Position = value;
        }
    }

    public override void Flush() => _inner.Flush();

    public override int Read(byte[] buffer, int offset, int count) => _inner.Read(buffer, offset, count);

    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        => _inner.ReadAsync(buffer, offset, count, cancellationToken);

    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        => _inner.ReadAsync(buffer, cancellationToken);

    public override long Seek(long offset, SeekOrigin origin) => _inner.Seek(offset, origin);

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _inner.Dispose();
            _response.Dispose();
        }

        base.Dispose(disposing);
    }
}
