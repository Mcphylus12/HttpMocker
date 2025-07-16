using HttpMocker;
using Microsoft.AspNetCore.Mvc;
using System.Net.Http.Json;
using System.Net.WebSockets;

namespace MockerTests;

public class Tests
{
    [Fact]
    public async Task Test()
    {
        var handler = Mocker.Create<DemoController>();
        var client = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://service-test"),
        };

        client.DefaultRequestHeaders.Add("x-header", "foo");

        var result = await client.PostAsJsonAsync("/api/test/hhh?queryParam=win", new TestBody
        {
            HelloWorld = 6
        });

        var resultBody = await result.Content.ReadFromJsonAsync<TestResult>();

        Assert.Equal(6, resultBody.Body.HelloWorld);
        Assert.Equal("foo", resultBody.Header);
        Assert.Equal("win", resultBody.Query);
        Assert.Equal("hhh", resultBody.Route);
    }
}


public class DemoController
{
    [Route("http://service-test/api/test/{id}")]
    [HttpPost]
    public static IActionResult Okay([FromRoute]string id, [FromQuery]string queryParam, [FromBody]TestBody body, [FromHeader(Name = "x-header")]string header)
    {
        return new OkObjectResult(new TestResult
        {
            Body = body,
            Route = id,
            Header = header,
            Query = queryParam
        });
    }
}

public class TestBody
{
    public int HelloWorld { get; set; }
}

public class TestResult
{
    public TestBody Body { get; set; }
    public string Route { get; set; }
    public string Query { get; set; }
    public string Header { get; set; }
}