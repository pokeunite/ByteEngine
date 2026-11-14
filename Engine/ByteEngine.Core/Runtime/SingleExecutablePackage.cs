using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ByteEngine.Core.Runtime;

/// <summary>
/// Append-only game payload for a self-contained .NET player ("one exe" exports).
///
/// The exported game folder contains a single executable: the published
/// single-file player with <c>Game.bytepak</c> appended to the end of the file
/// plus a fixed-size footer. Packaging hides loose source content; it is not DRM.
/// </summary>
public static class SingleExecutablePackage
{
    private static readonly byte[] Magic =
        Encoding.ASCII.GetBytes("BYTEEXE-PAYLOAD1");

    private const int FooterSize = 64;

    public readonly record struct Payload(long Offset, long Length, string Sha256);

    /// <summary>Appends a package to an executable and writes the payload footer.</summary>
    public static Payload Append(string executable, string package)
    {
        using FileStream input = File.OpenRead(package);
        byte[] hash = SHA256.HashData(input);
        input.Position = 0;

        using var output = new FileStream(executable, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        if (TryRead(output, out _))
            throw new InvalidDataException("Executable already contains a game payload.");

        long offset = output.Length;
        output.Position = offset;
        input.CopyTo(output);

        Span<byte> footer = stackalloc byte[FooterSize];
        Magic.CopyTo(footer);
        BinaryPrimitives.WriteInt64LittleEndian(footer[16..24], offset);
        BinaryPrimitives.WriteInt64LittleEndian(footer[24..32], input.Length);
        hash.CopyTo(footer[32..]);
        output.Write(footer);
        output.Flush(true);

        return new Payload(offset, input.Length, Convert.ToHexString(hash));
    }

    /// <summary>Reads the payload footer, if the stream is an executable with bundled content.</summary>
    public static bool TryRead(Stream stream, out Payload payload)
    {
        payload = default;
        if (!stream.CanSeek || stream.Length < FooterSize)
            return false;

        long position = stream.Position;
        try
        {
            stream.Position = stream.Length - FooterSize;
            Span<byte> footer = stackalloc byte[FooterSize];
            stream.ReadExactly(footer);
            if (!footer[..16].SequenceEqual(Magic))
                return false;

            long offset = BinaryPrimitives.ReadInt64LittleEndian(footer[16..24]);
            long length = BinaryPrimitives.ReadInt64LittleEndian(footer[24..32]);
            if (offset < 1 || length < 1 || offset > stream.Length - FooterSize ||
                length != stream.Length - FooterSize - offset)
                throw new InvalidDataException("Bundled game payload bounds are invalid.");

            payload = new Payload(offset, length, Convert.ToHexString(footer[32..]));
            return true;
        }
        finally
        {
            stream.Position = position;
        }
    }

    /// <summary>
    /// Extracts an appended payload to the per-user cache and returns that folder,
    /// or null when the executable carries no bundled content.
    /// </summary>
    public static string? Prepare(string executable, string? cacheRoot = null)
    {
        using FileStream input = File.OpenRead(executable);
        if (!TryRead(input, out Payload payload))
            return null;

        cacheRoot ??= Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ByteEngine", "Games", "Bundled");
        string folder = Path.Combine(cacheRoot, payload.Sha256);
        Directory.CreateDirectory(folder);
        string target = Path.Combine(folder, ByteAssetPackage.FileName);

        // Serialize publication across simultaneous launches, including the
        // manifest write: a reader validating the old target otherwise stops
        // Windows from replacing it.
        string lockId = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(Path.GetFullPath(folder).ToUpperInvariant())));
        using var publicationLock = new Mutex(false, "ByteEngine.Bundle." + lockId);
        bool acquired = false;
        try
        {
            try { acquired = publicationLock.WaitOne(TimeSpan.FromMinutes(2)); }
            catch (AbandonedMutexException) { acquired = true; }
            if (!acquired)
                throw new IOException("Another game launch is still preparing bundled content. Please wait and try again.");

            bool Valid()
            {
                try
                {
                    if (!File.Exists(target) || new FileInfo(target).Length != payload.Length)
                        return false;
                    using FileStream file = File.OpenRead(target);
                    return Convert.ToHexString(SHA256.HashData(file)) == payload.Sha256;
                }
                catch (Exception error) when (error is IOException or UnauthorizedAccessException)
                {
                    return false;
                }
            }

            if (!Valid())
            {
                string temporary = Path.Combine(folder, "payload-" + Guid.NewGuid().ToString("N") + ".tmp");
                try
                {
                    input.Position = payload.Offset;
                    using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                    {
                        byte[] buffer = new byte[128 * 1024];
                        long remaining = payload.Length;
                        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
                        while (remaining > 0)
                        {
                            int read = input.Read(buffer, 0, (int)Math.Min(buffer.Length, remaining));
                            if (read == 0)
                                throw new EndOfStreamException();
                            output.Write(buffer, 0, read);
                            hash.AppendData(buffer, 0, read);
                            remaining -= read;
                        }
                        if (Convert.ToHexString(hash.GetHashAndReset()) != payload.Sha256)
                            throw new InvalidDataException("Bundled game content failed its integrity check.");
                        output.Flush(true);
                    }

                    try
                    {
                        File.Move(temporary, target, true);
                    }
                    catch (Exception error) when (error is IOException or UnauthorizedAccessException)
                    {
                        // Older running builds, scanners or read-only stale files
                        // can hold the old cache. Publish verified bytes to a new
                        // directory instead of failing or using bad data.
                        folder = Path.Combine(folder, "recovery-" + Guid.NewGuid().ToString("N"));
                        Directory.CreateDirectory(folder);
                        target = Path.Combine(folder, ByteAssetPackage.FileName);
                        File.Move(temporary, target, false);
                    }
                }
                finally
                {
                    if (File.Exists(temporary))
                        File.Delete(temporary);
                }
            }

            // --validate uses the same content integrity path for loose and bundled games.
            string manifest = JsonSerializer.Serialize(new
            {
                formatVersion = 3,
                files = new[]
                {
                    new { Path = ByteAssetPackage.FileName, Length = payload.Length, Sha256 = payload.Sha256 }
                }
            });
            File.WriteAllText(Path.Combine(folder, "game-package.json"), manifest);
            return folder;
        }
        finally
        {
            if (acquired)
                publicationLock.ReleaseMutex();
        }
    }
}
