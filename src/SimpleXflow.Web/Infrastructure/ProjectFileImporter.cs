using System.Buffers;
using System.IO.Compression;
using System.Xml;
using System.Text.Json;
using System.Text;

namespace SimpleXflow.Web.Infrastructure;

/// <summary>Reads uploaded project files without writing archive entries to disk.</summary>
public static class ProjectFileImporter
{
    public const long MaxFileBytes = 20 * 1024 * 1024;
    private const string BpmnNamespace = "http://www.omg.org/spec/BPMN/20100524/MODEL";
    private const string InvalidProjectMessage = "The selected file is not a valid simpleXflow project file.";

    /// <summary>
    /// Leaves the supplied stream open. Both the upload and a selected archive entry are limited
    /// to 20 MiB so a small compressed file cannot cause an unbounded allocation when expanded.
    /// </summary>
    public static async Task<ImportedProject> ReadAsync(
        Stream upload, string fileName, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(upload);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);

        var extension = Path.GetExtension(fileName);
        var isZip = extension.Equals(".zip", StringComparison.OrdinalIgnoreCase);
        if (extension.Equals(".sxf", StringComparison.OrdinalIgnoreCase))
        {
            using var buffer = await ReadBoundedAsync(upload, cancellationToken);
            try
            {
                var document = await JsonSerializer.DeserializeAsync<ModelFile>(buffer,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true }, cancellationToken);
                if (document is null || document.Format != "simpleXflow" || document.Version != 1 || string.IsNullOrWhiteSpace(document.BpmnXml))
                    throw new InvalidOperationException(InvalidProjectMessage);
                using var architecture = new MemoryStream(Encoding.UTF8.GetBytes(document.BpmnXml));
                var xml = await ReadXmlAsync(architecture, cancellationToken);
                // Logic may be legacy JSON containing several per-process BPMN diagrams.
                // Preserve that payload exactly; it is interpreted by the modeler on import.
                var name = string.IsNullOrWhiteSpace(document.Name) ? GetProjectName(fileName) : document.Name.Trim();
                if (name.Length > 240) throw new InvalidOperationException("Model names can contain at most 240 characters.");
                return new ImportedProject(name, xml, document.LogicXml);
            }
            catch (JsonException exception) { throw new InvalidOperationException(InvalidProjectMessage, exception); }
        }
        if (!isZip && !extension.Equals(".bpmn", StringComparison.OrdinalIgnoreCase)
            && !extension.Equals(".xml", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Only simpleXflow model files with .sxf, .bpmn, .xml or .zip can be imported.");
        }

        using var uploadBuffer = await ReadBoundedAsync(upload, cancellationToken);
        if (!isZip)
        {
            var xml = await ReadXmlAsync(uploadBuffer, cancellationToken);
            return new ImportedProject(GetProjectName(fileName), xml, null);
        }

        using var archive = new ZipArchive(uploadBuffer, ZipArchiveMode.Read);
        var bpmnEntry = archive.Entries
            .Where(entry => !string.IsNullOrWhiteSpace(entry.Name)
                && Path.GetExtension(entry.Name).Equals(".bpmn", StringComparison.OrdinalIgnoreCase))
            .OrderBy(entry => entry.FullName.Count(character => character is '/' or '\\'))
            .ThenBy(entry => entry.FullName.Length)
            .FirstOrDefault()
            ?? throw new InvalidOperationException("The zip file does not contain a simpleXflow project file.");

        if (bpmnEntry.Length > MaxFileBytes)
        {
            throw new InvalidOperationException("The selected project exceeds the 20 MiB import limit.");
        }

        using var entryStream = bpmnEntry.Open();
        using var entryBuffer = await ReadBoundedAsync(entryStream, cancellationToken);
        var bpmnXml = await ReadXmlAsync(entryBuffer, cancellationToken);
        var nameSource = string.IsNullOrWhiteSpace(Path.GetFileNameWithoutExtension(bpmnEntry.Name))
            ? fileName
            : bpmnEntry.Name;
        return new ImportedProject(GetProjectName(nameSource), bpmnXml, null);
    }

    private static async Task<MemoryStream> ReadBoundedAsync(Stream input, CancellationToken cancellationToken)
    {
        var output = new MemoryStream();
        var buffer = ArrayPool<byte>.Shared.Rent(81920);
        try
        {
            int count;
            while ((count = await input.ReadAsync(buffer.AsMemory(), cancellationToken)) != 0)
            {
                if (output.Length + count > MaxFileBytes)
                {
                    throw new InvalidOperationException("The selected project exceeds the 20 MiB import limit.");
                }

                output.Write(buffer, 0, count);
            }

            output.Position = 0;
            return output;
        }
        catch
        {
            output.Dispose();
            throw;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private static async Task<string> ReadXmlAsync(Stream stream, CancellationToken cancellationToken)
    {
        using var textReader = new StreamReader(stream, leaveOpen: true);
        var xml = await textReader.ReadToEndAsync(cancellationToken);
        try
        {
            using var reader = XmlReader.Create(new StringReader(xml), new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null,
                MaxCharactersInDocument = MaxFileBytes
            });
            if (reader.MoveToContent() != XmlNodeType.Element
                || reader.LocalName != "definitions" || reader.NamespaceURI != BpmnNamespace)
            {
                throw new InvalidOperationException(InvalidProjectMessage);
            }

            // Consume the complete document: a valid root does not imply a well-formed body.
            while (reader.Read())
            {
                cancellationToken.ThrowIfCancellationRequested();
            }
        }
        catch (XmlException exception)
        {
            throw new InvalidOperationException(InvalidProjectMessage, exception);
        }

        return xml;
    }

    private static string GetProjectName(string fileName)
    {
        var name = Path.GetFileNameWithoutExtension(fileName.Replace('\\', '/')).Trim();
        return string.IsNullOrWhiteSpace(name) ? "Imported project" : name;
    }

    private sealed record ModelFile(string Format, int Version, string? Name, string BpmnXml, string? LogicXml);
}

public sealed record ImportedProject(string Name, string BpmnXml, string? LogicXml);
