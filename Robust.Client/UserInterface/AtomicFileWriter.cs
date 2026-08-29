using System;
using System.IO;
using System.Threading.Tasks;
using Robust.Client.Graphics;

namespace Robust.Client.UserInterface;

internal static class AtomicFileWriter
{
    internal static async Task<bool> SaveAsync(
        IFileDialogManagerImplementation? dialog,
        ReadOnlyMemory<byte> data,
        FileDialogFilters? filters = null,
        Func<string, ReadOnlyMemory<byte>, ValueTask>? writeTemporary = null)
    {
        if (dialog == null)
            return false;

        var destinationPath = await dialog.SaveFile(filters);
        if (destinationPath == null)
            return false;

        await WriteAsync(destinationPath, data, writeTemporary);
        return true;
    }

    internal static async Task WriteAsync(
        string destinationPath,
        ReadOnlyMemory<byte> data,
        Func<string, ReadOnlyMemory<byte>, ValueTask>? writeTemporary = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationPath);

        var fullDestinationPath = Path.GetFullPath(destinationPath);
        var directory = Path.GetDirectoryName(fullDestinationPath)
            ?? throw new ArgumentException("The destination must have a parent directory.", nameof(destinationPath));
        var temporaryPath = Path.Combine(
            directory,
            $".{Path.GetFileName(fullDestinationPath)}.{Guid.NewGuid():N}.tmp");

        try
        {
            if (writeTemporary == null)
                await WriteTemporaryAsync(temporaryPath, data);
            else
                await writeTemporary(temporaryPath, data);

            if (!File.Exists(temporaryPath))
                throw new IOException("The temporary writer did not create its output file.");

            // Both paths share a directory, so the move cannot degrade into a cross-volume copy.
            File.Move(temporaryPath, fullDestinationPath, overwrite: true);
        }
        catch (Exception operationException)
        {
            try
            {
                File.Delete(temporaryPath);
            }
            catch (Exception cleanupException)
            {
                throw new AggregateException(
                    "Atomic file writing failed and its temporary file could not be removed.",
                    operationException,
                    cleanupException);
            }

            throw;
        }
    }

    private static async ValueTask WriteTemporaryAsync(
        string temporaryPath,
        ReadOnlyMemory<byte> data)
    {
        await using var stream = new FileStream(temporaryPath, new FileStreamOptions
        {
            Mode = FileMode.CreateNew,
            Access = FileAccess.Write,
            Share = FileShare.None,
            Options = FileOptions.Asynchronous | FileOptions.SequentialScan,
        });

        await stream.WriteAsync(data);
        await stream.FlushAsync();
        stream.Flush(flushToDisk: true);
    }
}
