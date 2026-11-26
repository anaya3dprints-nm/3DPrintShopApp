using System.Net;
using System.Net.Http.Json;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.FileProviders;
using ThreeDPrintStore.Services;
using Xunit;

namespace ThreeDPrintStore.Tests;

public class UploadTests
{
    [Fact]
    public async Task Api_requires_antiforgery_and_rejects_bad_extension_and_invalid_fields()
    {
        using var factory = new StoreFactory();
        using var client = factory.CreateStoreClient();
        using var noToken = new MultipartFormDataContent();
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync("/api/PrintRequests/submit", noToken)).StatusCode);
        var antiforgery = await client.GetFromJsonAsync<Antiforgery>("/api/PrintRequests/antiforgery");
        Assert.NotNull(antiforgery);
        using var form = new MultipartFormDataContent();
        form.Add(new StringContent(antiforgery.Token), antiforgery.FormFieldName);
        form.Add(new StringContent("Customer"), "CustomerName");
        form.Add(new StringContent("customer@example.com"), "CustomerEmail");
        form.Add(new StringContent("A project"), "ProjectDescription");
        form.Add(new StringContent("1"), "RequestedColorsCount");
        form.Add(new ByteArrayContent(Encoding.UTF8.GetBytes("malicious content")), "ModelFile", "model.exe");
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync("/api/PrintRequests/submit", form)).StatusCode);
    }

    [Fact]
    public async Task Private_store_validates_type_size_and_content_and_uses_safe_generated_names()
    {
        var root = Path.Combine(AppContext.BaseDirectory, "test-data", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var store = new PrivateUploadStore(new TestEnvironment(root));
            await Assert.ThrowsAsync<InvalidDataException>(() => store.SaveAsync(File("bad.exe", "data"), false, default));
            await Assert.ThrowsAsync<InvalidDataException>(() => store.SaveAsync(File("bad.stl", "not an stl"), false, default));
            await Assert.ThrowsAsync<InvalidDataException>(() => store.SaveAsync(File("bad.png", "not a png"), true, default));
            var tooLarge = new FormFile(Stream.Null, 0, PrivateUploadStore.ModelLimit + 1, "model", "large.stl");
            await Assert.ThrowsAsync<InvalidDataException>(() => store.SaveAsync(tooLarge, false, default));

            var name = await store.SaveAsync(File("../../model.stl", "solid test\nfacet normal 0 0 0\nendsolid test"), false, default);
            Assert.Equal(name, Path.GetFileName(name));
            Assert.EndsWith(".stl", name);
            Assert.NotNull(store.Find(name, false));
            Assert.Null(store.Find("../" + name, false));
            store.Delete(name);
            Assert.Null(store.Find(name, false));
            Assert.Empty(Directory.GetFiles(Path.Combine(root, "App_Data", "Uploads")));
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    private static IFormFile File(string name, string content)
    {
        var bytes = Encoding.UTF8.GetBytes(content);
        return new FormFile(new MemoryStream(bytes), 0, bytes.Length, "file", name);
    }

    private sealed record Antiforgery(string Token, string FormFieldName);

    private sealed class TestEnvironment(string root) : IWebHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Testing";
        public string ApplicationName { get; set; } = "ThreeDPrintStore";
        public string ContentRootPath { get; set; } = root;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = Path.Combine(root, "wwwroot");
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
    }
}
