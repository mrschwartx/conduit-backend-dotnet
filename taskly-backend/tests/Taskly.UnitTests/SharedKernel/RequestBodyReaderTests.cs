using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Taskly.SharedKernel.Domain;
using Taskly.SharedKernel.Infrastructure.Http;

namespace Taskly.UnitTests.SharedKernel;

public sealed class RequestBodyReaderTests
{
    [Fact]
    public async Task ReadsJsonObjectsKeepingTypes()
    {
        var request = CreateRequest("{\"name\":\"x\",\"count\":3,\"missing\":null,\"nested\":{\"a\":1}}", "application/json");

        var body = await RequestBodyReader.ReadJsonAsync(request, CancellationToken.None);

        Assert.Equal("x", body["name"]);
        Assert.IsType<JsonElement>(body["count"]);
        Assert.True(body.ContainsKey("missing"));
        Assert.Null(body["missing"]);
        Assert.IsType<JsonElement>(body["nested"]);
    }

    [Fact]
    public async Task TreatsOtherContentTypesAndBlankBodiesAsEmpty()
    {
        var form = await RequestBodyReader.ReadJsonAsync(CreateRequest("a=b", "application/x-www-form-urlencoded"), CancellationToken.None);
        var blank = await RequestBodyReader.ReadJsonAsync(CreateRequest("  \n", "application/json"), CancellationToken.None);
        var none = await RequestBodyReader.ReadJsonAsync(CreateRequest(string.Empty, null), CancellationToken.None);

        Assert.Empty(form);
        Assert.Empty(blank);
        Assert.Empty(none);
    }

    [Fact]
    public async Task RejectsMalformedAndNonObjectBodies()
    {
        foreach (var payload in new[] { "{broken", "[]", "\"text\"", "42" })
        {
            var error = await Assert.ThrowsAsync<ValidationException>(() =>
                RequestBodyReader.ReadJsonAsync(CreateRequest(payload, "application/json"), CancellationToken.None));
            Assert.Equal("Malformed JSON body", error.Message);
        }
    }

    [Fact]
    public async Task RejectsOversizedBodies()
    {
        var payload = "{\"value\":\"" + new string('a', RequestBodyReader.MaxBytes) + "\"}";

        await Assert.ThrowsAsync<PayloadTooLargeException>(() =>
            RequestBodyReader.ReadJsonAsync(CreateRequest(payload, "application/json"), CancellationToken.None));
    }

    [Theory]
    [InlineData("application/json", true)]
    [InlineData("Application/JSON; charset=utf-8", true)]
    [InlineData("application/vnd.api+json", true)]
    [InlineData("text/plain", false)]
    [InlineData("", false)]
    public void DetectsJsonContentTypes(string contentType, bool expected)
    {
        Assert.Equal(expected, RequestBodyReader.IsJson(contentType));
    }

    private static HttpRequest CreateRequest(string body, string? contentType)
    {
        var context = new DefaultHttpContext();
        var bytes = Encoding.UTF8.GetBytes(body);
        context.Request.Body = new MemoryStream(bytes);
        context.Request.ContentType = contentType;
        return context.Request;
    }
}
