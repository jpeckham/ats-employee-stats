using System.Text;
using SIIDecryptSharp;

namespace AtsEmployeeStats.Infrastructure.Saves;

public sealed class SiiSaveTextDecoder
{
    public async Task<string> DecodeAsync(string path, CancellationToken cancellationToken)
    {
        var header = new byte[4];
        int read;
        await using (var headerStream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete))
        {
            read = await headerStream.ReadAsync(header, cancellationToken);
        }

        if (read >= 4 && Encoding.ASCII.GetString(header) == "ScsC")
        {
            var decrypted = Decryptor.Decrypt(path, true);
            return Encoding.UTF8.GetString(decrypted);
        }

        await using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete);
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        return await reader.ReadToEndAsync(cancellationToken);
    }
}
