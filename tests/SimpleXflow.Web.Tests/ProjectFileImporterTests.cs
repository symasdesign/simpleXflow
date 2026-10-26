using System.IO.Compression;
using System.Text;
using System.Text.Json;
using SimpleXflow.Web.Infrastructure;

namespace SimpleXflow.Web.Tests;

public sealed class ProjectFileImporterTests
{
    [Fact]
    public async Task Import_LocalSave_PreservesArchitectureAndPerProcessLogic()
    {
        const string logic = "{\"Task_1\":\"<definitions />\"}";
        var json = JsonSerializer.Serialize(new { format = "simpleXflow", version = 1, name = "Local model", bpmnXml = BpmnXml, logicXml = logic });
        using var upload = TextStream(json);
        var model = await ProjectFileImporter.ReadAsync(upload, "my-backup.sxf");
        Assert.Equal("Local model", model.Name);
        Assert.Equal(BpmnXml, model.BpmnXml);
        Assert.Equal(logic, model.LogicXml);
    }

    [Theory]
    [InlineData("{\"format\":\"simpleXflow\",\"version\":2,\"bpmnXml\":\"x\"}")]
    [InlineData("{\"format\":\"simpleXflow\",\"version\":1,\"bpmnXml\":\"<not-bpmn/>\"}")]
    [InlineData("null")]
    [InlineData("{broken")]
    public async Task Import_InvalidLocalSave_IsRejected(string json)
    {
        using var upload = TextStream(json);
        await Assert.ThrowsAsync<InvalidOperationException>(() => ProjectFileImporter.ReadAsync(upload, "model.sxf"));
    }

    private const string BpmnXml = """
        <?xml version="1.0" encoding="utf-8"?>
        <bpmn:definitions xmlns:bpmn="http://www.omg.org/spec/BPMN/20100524/MODEL">
          <bpmn:process id="Process_1"><bpmn:startEvent id="Start_1" /></bpmn:process>
        </bpmn:definitions>
        """;

    [Theory]
    [InlineData("Process.bpmn", "Process")]
    [InlineData("Model.XML", "Model")]
    [InlineData("Architecture.BPMN", "Architecture")]
    [InlineData("  Workspace  .xml", "Workspace")]
    [InlineData(".bpmn", "Imported project")]
    public async Task Import_ValidModel_PreservesXmlAndDerivesProjectName(string fileName, string expectedName)
    {
        using var upload = TextStream(BpmnXml);

        var project = await ProjectFileImporter.ReadAsync(upload, fileName);

        Assert.Equal(expectedName, project.Name);
        Assert.Equal(BpmnXml, project.BpmnXml);
        Assert.Null(project.LogicXml);
        Assert.True(upload.CanRead);
    }

    [Theory]
    [InlineData("model.txt")]
    [InlineData("model.json")]
    [InlineData("model.bpmn.exe")]
    [InlineData("model")]
    public async Task Import_UnsupportedExtension_IsRejectedBeforeReading(string fileName)
    {
        using var upload = TextStream(BpmnXml);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => ProjectFileImporter.ReadAsync(upload, fileName));

        Assert.Contains(".bpmn, .xml or .zip", exception.Message);
        Assert.Equal(0, upload.Position);
        Assert.True(upload.CanRead);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  \r\n  ")]
    [InlineData("garbage with <bpmn in the middle")]
    [InlineData("<!-- <bpmn:definitions --> <unrelated />")]
    [InlineData("<definitions />")]
    [InlineData("<definitions xmlns='urn:wrong' />")]
    [InlineData("<bpmn:definitions />")]
    [InlineData("<root><definitions xmlns='http://www.omg.org/spec/BPMN/20100524/MODEL' /></root>")]
    [InlineData("<definitions xmlns='http://www.omg.org/spec/BPMN/20100524/MODEL'><unclosed></definitions>")]
    [InlineData("<definitions xmlns='http://www.omg.org/spec/BPMN/20100524/MODEL' /> trailing garbage")]
    [InlineData("<definitions xmlns='http://www.omg.org/spec/BPMN/20100524/MODEL' /><second-root />")]
    public async Task Import_MalformedOrUnrelatedXml_IsRejected(string xml)
    {
        using var upload = TextStream(xml);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => ProjectFileImporter.ReadAsync(upload, "invalid.bpmn"));

        Assert.Contains("not a valid simpleXflow project", exception.Message);
    }

    [Theory]
    [InlineData("<!DOCTYPE definitions [<!ENTITY value 'expanded'>]><definitions xmlns='http://www.omg.org/spec/BPMN/20100524/MODEL'>&value;</definitions>")]
    [InlineData("<!DOCTYPE definitions SYSTEM 'file:///must-not-be-read'><definitions xmlns='http://www.omg.org/spec/BPMN/20100524/MODEL' />")]
    public async Task Import_DtdAndEntities_AreRejected(string xml)
    {
        using var upload = TextStream(xml);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => ProjectFileImporter.ReadAsync(upload, "invalid.xml"));

        Assert.Contains("not a valid simpleXflow project", exception.Message);
    }

    [Theory]
    [InlineData("<definitions xmlns='http://www.omg.org/spec/BPMN/20100524/MODEL' />")]
    [InlineData("<!-- Model --><custom:definitions xmlns:custom='http://www.omg.org/spec/BPMN/20100524/MODEL' />")]
    public async Task Import_BpmnNamespace_IsIndependentOfPrefix(string xml)
    {
        using var upload = TextStream(xml);

        var project = await ProjectFileImporter.ReadAsync(upload, "model.xml");

        Assert.Equal(xml, project.BpmnXml);
    }

    [Fact]
    public async Task Import_Utf16BomAndUnicode_AreDecodedWithoutChangingXml()
    {
        var xml = BpmnXml.Replace("utf-8", "utf-16").Replace("Process_1", "Prüfung_1");
        using var upload = new MemoryStream([.. Encoding.Unicode.GetPreamble(), .. Encoding.Unicode.GetBytes(xml)]);

        var project = await ProjectFileImporter.ReadAsync(upload, "Prüfung.xml");

        Assert.Equal(xml, project.BpmnXml);
        Assert.Equal("Prüfung", project.Name);
    }

    [Fact]
    public async Task Import_NonSeekableUploadWithPartialReads_IsSupported()
    {
        using var upload = new NonSeekableReadStream(Encoding.UTF8.GetBytes(BpmnXml));

        var project = await ProjectFileImporter.ReadAsync(upload, "model.bpmn");

        Assert.Equal(BpmnXml, project.BpmnXml);
        Assert.True(upload.CanRead);
    }

    [Fact]
    public async Task Import_CancelledRead_PropagatesCancellationAndLeavesUploadOpen()
    {
        using var upload = TextStream(BpmnXml);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            ProjectFileImporter.ReadAsync(upload, "model.bpmn", cancellation.Token));

        Assert.True(upload.CanRead);
    }

    [Fact]
    public async Task ZipImport_PrefersRootModelOverShorterNestedModelAndIgnoresDirectories()
    {
        using var upload = CreateZip(
            ("nested/a.bpmn", "not the selected model"),
            ("directory.bpmn/", ""),
            ("Architecture.bpmn", BpmnXml),
            ("readme.txt", "not XML"));

        var project = await ProjectFileImporter.ReadAsync(upload, "bundle.ZIP");

        Assert.Equal("Architecture", project.Name);
        Assert.Equal(BpmnXml, project.BpmnXml);
        Assert.True(upload.CanRead);
    }

    [Fact]
    public async Task ZipImport_PrefersShortestPathAtTheSameDepth()
    {
        using var upload = CreateZip(
            ("directory/long-model.bpmn", "not selected"),
            ("sub/x.BPMN", BpmnXml));

        var project = await ProjectFileImporter.ReadAsync(upload, "bundle.zip");

        Assert.Equal("x", project.Name);
        Assert.Equal(BpmnXml, project.BpmnXml);
    }

    [Theory]
    [InlineData("nested/Plan.bpmn")]
    [InlineData("nested\\Plan.bpmn")]
    [InlineData("../Plan.bpmn")]
    public async Task ZipImport_UsesEntryBaseNameAndNeverExtractsItsPath(string entryName)
    {
        using var upload = CreateZip((entryName, BpmnXml));

        var project = await ProjectFileImporter.ReadAsync(upload, "bundle.zip");

        Assert.Equal("Plan", project.Name);
        Assert.Equal(BpmnXml, project.BpmnXml);
    }

    [Fact]
    public async Task ZipImport_UnnamedModel_FallsBackToArchiveName()
    {
        using var upload = CreateZip((".bpmn", BpmnXml));

        var project = await ProjectFileImporter.ReadAsync(upload, "Bundle.zip");

        Assert.Equal("Bundle", project.Name);
    }

    [Theory]
    [InlineData("readme.txt")]
    [InlineData("model.xml")]
    [InlineData("directory.bpmn/")]
    public async Task ZipImport_WithoutBpmnFile_IsRejected(string entryName)
    {
        using var upload = CreateZip((entryName, BpmnXml));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => ProjectFileImporter.ReadAsync(upload, "bundle.zip"));

        Assert.Contains("does not contain", exception.Message);
    }

    [Fact]
    public async Task ZipImport_CorruptedArchive_IsRejected()
    {
        using var upload = TextStream("This is not a zip archive.");

        await Assert.ThrowsAsync<InvalidDataException>(() => ProjectFileImporter.ReadAsync(upload, "invalid.zip"));
    }

    [Fact]
    public async Task ZipImport_InvalidSelectedModel_IsRejectedInsteadOfSilentlyChoosingAnother()
    {
        using var upload = CreateZip(("a.bpmn", "not valid <bpmn XML"), ("fallback.bpmn", BpmnXml));

        await Assert.ThrowsAsync<InvalidOperationException>(() => ProjectFileImporter.ReadAsync(upload, "bundle.zip"));
    }

    [Theory]
    [InlineData("model.bpmn")]
    [InlineData("archive.zip")]
    public async Task Import_UploadLargerThanLimit_IsRejected(string fileName)
    {
        using var upload = new MemoryStream(new byte[ProjectFileImporter.MaxFileBytes + 1]);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => ProjectFileImporter.ReadAsync(upload, fileName));

        Assert.Contains("20 MiB import limit", exception.Message);
        Assert.True(upload.CanRead);
    }

    [Fact]
    public async Task Import_ValidXmlExactlyAtLimit_IsAccepted()
    {
        var xml = BpmnXml + new string(' ', checked((int)ProjectFileImporter.MaxFileBytes - Encoding.UTF8.GetByteCount(BpmnXml)));
        using var upload = TextStream(xml);

        var project = await ProjectFileImporter.ReadAsync(upload, "limit.bpmn");

        Assert.Equal(xml, project.BpmnXml);
    }

    [Fact]
    public async Task ZipImport_SmallArchiveWithOversizedExpandedEntry_IsRejected()
    {
        using var upload = CreateZip(("large.bpmn", BpmnXml + new string(' ', checked((int)ProjectFileImporter.MaxFileBytes))));
        Assert.True(upload.Length < 100_000);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => ProjectFileImporter.ReadAsync(upload, "bomb.zip"));

        Assert.Contains("20 MiB import limit", exception.Message);
    }

    private static MemoryStream TextStream(string text) => new(Encoding.UTF8.GetBytes(text));

    private static MemoryStream CreateZip(params (string Name, string Content)[] entries)
    {
        var result = new MemoryStream();
        using (var archive = new ZipArchive(result, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (name, content) in entries)
            {
                var entry = archive.CreateEntry(name, CompressionLevel.Optimal);
                using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false));
                writer.Write(content);
            }
        }

        result.Position = 0;
        return result;
    }

    private sealed class NonSeekableReadStream(byte[] content) : Stream
    {
        private readonly MemoryStream inner = new(content);
        public override bool CanRead => inner.CanRead;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override int Read(byte[] buffer, int offset, int count) => inner.Read(buffer, offset, Math.Min(count, 7));
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            inner.ReadAsync(buffer[..Math.Min(buffer.Length, 7)], cancellationToken);
        public override void Flush() => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        protected override void Dispose(bool disposing)
        {
            if (disposing) inner.Dispose();
            base.Dispose(disposing);
        }
    }
}
