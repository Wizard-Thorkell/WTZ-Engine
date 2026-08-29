using System.Text;
using NUnit.Framework;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;

namespace Robust.Client.Tests.UserInterface;

[TestFixture]
internal sealed class AtomicFileWriterTest
{
    private string _directoryPath = default!;

    [SetUp]
    public void SetUp()
    {
        _directoryPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_directoryPath);
    }

    [TearDown]
    public void TearDown()
    {
        Directory.Delete(_directoryPath, recursive: true);
    }

    [Test]
    public async Task CreatesNewDestination()
    {
        var destination = DestinationPath();
        var data = "new map"u8.ToArray();

        await AtomicFileWriter.WriteAsync(destination, data);

        Assert.Multiple(() =>
        {
            Assert.That(File.ReadAllBytes(destination), Is.EqualTo(data));
            AssertOnlyDestinationRemains(destination);
        });
    }

    [Test]
    public async Task ReplacesExistingDestination()
    {
        var destination = DestinationPath();
        await File.WriteAllTextAsync(destination, "old map");
        var data = "replacement"u8.ToArray();

        await AtomicFileWriter.WriteAsync(destination, data);

        Assert.Multiple(() =>
        {
            Assert.That(File.ReadAllBytes(destination), Is.EqualTo(data));
            AssertOnlyDestinationRemains(destination);
        });
    }

    [Test]
    public async Task WriteFailurePreservesDestinationAndCleansTemporaryFile()
    {
        var destination = DestinationPath();
        var original = "known good map"u8.ToArray();
        await File.WriteAllBytesAsync(destination, original);
        string? temporaryPath = null;

        var exception = Assert.ThrowsAsync<IOException>(async () =>
            await AtomicFileWriter.WriteAsync(
                destination,
                "partial replacement"u8.ToArray(),
                async (path, data) =>
                {
                    temporaryPath = path;
                    await File.WriteAllBytesAsync(path, data[..4].ToArray());
                    throw new IOException("Injected temporary write failure.");
                }));

        Assert.Multiple(() =>
        {
            Assert.That(exception!.Message, Is.EqualTo("Injected temporary write failure."));
            Assert.That(File.ReadAllBytes(destination), Is.EqualTo(original));
            Assert.That(temporaryPath, Is.Not.Null);
            Assert.That(File.Exists(temporaryPath), Is.False);
            AssertOnlyDestinationRemains(destination);
        });
    }

    [Test]
    public async Task PreservesUnicodeBytes()
    {
        var destination = DestinationPath();
        var encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
        var text = "floor: t\u00E9rreo\nlabel: \u5730\u4E0B\n";
        var data = encoding.GetBytes(text);

        await AtomicFileWriter.WriteAsync(destination, data);

        Assert.Multiple(() =>
        {
            Assert.That(File.ReadAllBytes(destination), Is.EqualTo(data));
            Assert.That(File.ReadAllText(destination, encoding), Is.EqualTo(text));
            AssertOnlyDestinationRemains(destination);
        });
    }

    [Test]
    public async Task CancelledDialogDoesNotWrite()
    {
        var writeCalled = false;
        var saved = await AtomicFileWriter.SaveAsync(
            new StubFileDialog(null),
            "ignored"u8.ToArray(),
            writeTemporary: (_, _) =>
            {
                writeCalled = true;
                return ValueTask.CompletedTask;
            });

        Assert.Multiple(() =>
        {
            Assert.That(saved, Is.False);
            Assert.That(writeCalled, Is.False);
            Assert.That(Directory.GetFiles(_directoryPath), Is.Empty);
        });
    }

    private string DestinationPath()
    {
        return Path.Combine(_directoryPath, "mapping.yml");
    }

    private void AssertOnlyDestinationRemains(string destination)
    {
        Assert.That(Directory.GetFiles(_directoryPath), Is.EqualTo(new[] { destination }));
    }

    private sealed class StubFileDialog(string? savePath) : IFileDialogManagerImplementation
    {
        public Task<string?> OpenFile(FileDialogFilters? filters)
        {
            throw new NotSupportedException();
        }

        public Task<string?> SaveFile(FileDialogFilters? filters)
        {
            return Task.FromResult(savePath);
        }
    }
}
