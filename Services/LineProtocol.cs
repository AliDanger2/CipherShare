using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CipherShare.Services;

/// <summary>
/// The transfer protocol mixes small JSON "lines" (the header, the per-file trailer) with
/// raw binary file bytes on the same TCP stream. A regular StreamReader is dangerous here
/// because it reads ahead into its own buffer and can accidentally swallow bytes that
/// belong to the next file. These two helpers read/write one line at a time using only the
/// exact bytes they need, so the raw binary reads that follow always start in the right place.
/// </summary>
public static class LineProtocol
{
    public static async Task WriteLineAsync(Stream stream, string line, CancellationToken token = default)
    {
        var bytes = Encoding.UTF8.GetBytes(line + "\n");
        await stream.WriteAsync(bytes, 0, bytes.Length, token).ConfigureAwait(false);
        await stream.FlushAsync(token).ConfigureAwait(false);
    }

    public static async Task<string> ReadLineAsync(Stream stream, CancellationToken token = default)
    {
        var buffer = new MemoryStream();
        var singleByte = new byte[1];

        while (true)
        {
            int read = await stream.ReadAsync(singleByte, 0, 1, token).ConfigureAwait(false);
            if (read == 0)
            {
                // Connection closed before we got a full line.
                return buffer.Length == 0 ? null : Encoding.UTF8.GetString(buffer.ToArray());
            }

            if (singleByte[0] == (byte)'\n')
            {
                return Encoding.UTF8.GetString(buffer.ToArray());
            }

            buffer.WriteByte(singleByte[0]);
        }
    }
}
