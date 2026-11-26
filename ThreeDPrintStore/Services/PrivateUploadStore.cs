using System.IO.Compression;
using System.Text;
using System.Xml;

namespace ThreeDPrintStore.Services;

public sealed class PrivateUploadStore
{
    public const long ModelLimit = 25 * 1024 * 1024;
    public const long ImageLimit = 5 * 1024 * 1024;
    public const long RequestLimit = 31 * 1024 * 1024;
    private readonly string _privateRoot;
    private readonly string _legacyRoot;

    public PrivateUploadStore(IWebHostEnvironment environment)
    {
        _privateRoot = Path.Combine(environment.ContentRootPath, "App_Data", "Uploads");
        _legacyRoot = Path.Combine(environment.WebRootPath ?? Path.Combine(environment.ContentRootPath, "wwwroot"), "uploads");
    }

    public async Task<string> SaveAsync(IFormFile file, bool image, CancellationToken cancellationToken)
    {
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        var allowed = image ? new[] { ".jpg", ".jpeg", ".png", ".webp" } : new[] { ".stl", ".obj", ".3mf" };
        var limit = image ? ImageLimit : ModelLimit;
        if (!allowed.Contains(extension) || file.Length <= 0 || file.Length > limit)
            throw new InvalidDataException(image ? "Use a JPG, PNG or WebP image up to 5 MB." : "Use an STL, OBJ or 3MF model up to 25 MB.");

        Directory.CreateDirectory(_privateRoot);
        var name = Guid.NewGuid().ToString("N") + extension;
        var path = Path.Combine(_privateRoot, name);
        try
        {
            await using (var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            await using (var input = file.OpenReadStream())
            {
                var buffer = new byte[81920];
                long total = 0;
                int read;
                while ((read = await input.ReadAsync(buffer, cancellationToken)) > 0)
                {
                    total += read;
                    if (total > limit) throw new InvalidDataException("File exceeds the size limit.");
                    await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                }
                if (total != file.Length) throw new InvalidDataException("The uploaded file was incomplete.");
            }
            if (!ValidContent(path, extension))
                throw new InvalidDataException("File content does not match the selected format.");
            return name;
        }
        catch
        {
            System.IO.File.Delete(path);
            throw;
        }
    }

    private static bool ValidContent(string path, string extension)
    {
        using var stream = System.IO.File.OpenRead(path);
        var header = new byte[Math.Min(84, (int)stream.Length)];
        stream.ReadExactly(header);
        if (extension is ".jpg" or ".jpeg")
        {
            if (stream.Length < 4 || header[0] != 0xff || header[1] != 0xd8 || header[2] != 0xff) return false;
            stream.Position = stream.Length - 2;
            return stream.ReadByte() == 0xff && stream.ReadByte() == 0xd9;
        }
        if (extension == ".png")
        {
            if (stream.Length < 45 || !header.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 })
                || !header.AsSpan(12, 4).SequenceEqual("IHDR"u8)) return false;
            stream.Position = stream.Length - 12;
            var tail = new byte[12];
            stream.ReadExactly(tail);
            return tail.AsSpan(0, 8).SequenceEqual(new byte[] { 0, 0, 0, 0, 73, 69, 78, 68 });
        }
        if (extension == ".webp")
            return header.Length >= 16 && Encoding.ASCII.GetString(header, 0, 4) == "RIFF"
                && Encoding.ASCII.GetString(header, 8, 4) == "WEBP"
                && System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(4, 4)) + 8L == stream.Length
                && (header.AsSpan(12, 4).SequenceEqual("VP8 "u8) || header.AsSpan(12, 4).SequenceEqual("VP8L"u8)
                    || header.AsSpan(12, 4).SequenceEqual("VP8X"u8));
        if (extension == ".3mf")
        {
            try
            {
                stream.Position = 0;
                using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
                if (archive.Entries.Count is 0 or > 10000
                    || !archive.Entries.Any(e => e.FullName.Equals("[Content_Types].xml", StringComparison.OrdinalIgnoreCase))) return false;
                var model = archive.Entries.FirstOrDefault(e => e.FullName.StartsWith("3D/", StringComparison.OrdinalIgnoreCase)
                    && e.FullName.EndsWith(".model", StringComparison.OrdinalIgnoreCase));
                if (model == null || model.Length is <= 0 or > 10 * 1024 * 1024) return false;
                using var xml = XmlReader.Create(model.Open(), new XmlReaderSettings
                {
                    DtdProcessing = DtdProcessing.Prohibit,
                    XmlResolver = null,
                    MaxCharactersInDocument = 10 * 1024 * 1024
                });
                if (xml.MoveToContent() != XmlNodeType.Element || xml.LocalName != "model"
                    || xml.NamespaceURI != "http://schemas.microsoft.com/3dmanufacturing/core/2015/02") return false;
                while (xml.Read()) { }
                return true;
            }
            catch (Exception ex) when (ex is InvalidDataException or IOException or XmlException) { return false; }
        }
        if (extension == ".stl" && header.Length == 84)
        {
            var triangles = System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(80, 4));
            if (triangles > 0 && 84L + 50L * triangles == stream.Length) return true;
        }
        stream.Position = 0;
        using var reader = new StreamReader(stream, new UTF8Encoding(false, true));
        try
        {
            var text = reader.ReadToEnd();
            if (text.Contains('\0')) return false;
            if (extension == ".stl")
                return text.TrimStart().StartsWith("solid ", StringComparison.OrdinalIgnoreCase)
                    && text.Contains("facet normal", StringComparison.OrdinalIgnoreCase)
                    && text.Contains("endsolid", StringComparison.OrdinalIgnoreCase);
            var lines = text.Split('\n');
            return lines.Any(l => l.TrimStart().StartsWith("v ", StringComparison.Ordinal))
                && lines.Any(l => l.TrimStart().StartsWith("f ", StringComparison.Ordinal));
        }
        catch (DecoderFallbackException) { return false; }
    }

    public void Delete(string? name)
    {
        if (!string.IsNullOrEmpty(name) && Path.GetFileName(name) == name)
            System.IO.File.Delete(Path.Combine(_privateRoot, name));
    }

    public string? Find(string? storedPath, bool reference)
    {
        if (string.IsNullOrWhiteSpace(storedPath)) return null;
        var value = storedPath.Replace('\\', '/');
        string name;
        if (value.StartsWith("/uploads/", StringComparison.OrdinalIgnoreCase))
        {
            var prefix = reference ? "/uploads/references/" : "/uploads/";
            if (!value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return null;
            name = value[prefix.Length..];
        }
        else if (Path.IsPathRooted(storedPath))
        {
            var full = Path.GetFullPath(storedPath);
            if (!string.Equals(Path.GetDirectoryName(full), Path.GetFullPath(_privateRoot), StringComparison.Ordinal)) return null;
            name = Path.GetFileName(full);
        }
        else name = value;
        if (string.IsNullOrEmpty(name) || name is "." or ".." || name.Contains('/') || name.Contains('\\')) return null;
        var privatePath = Path.Combine(_privateRoot, name);
        var legacyPath = Path.Combine(_legacyRoot, reference ? "references" : "", name);
        foreach (var candidate in new[] { privatePath, legacyPath })
        {
            if (System.IO.File.Exists(candidate) && (System.IO.File.GetAttributes(candidate) & FileAttributes.ReparsePoint) == 0)
                return candidate;
        }
        return null;
    }
}
