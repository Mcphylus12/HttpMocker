using HttpMocker;
using Microsoft.AspNetCore.Mvc;
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
            BaseAddress = new Uri("http://service-test")
        };

        await client.GetAsync("/api/test?test=win");
    }
}


public class DemoController
{
    [Route("http://service-test/api/test")]
    [HttpGet]
    public static void Okay()
    {

    }
}