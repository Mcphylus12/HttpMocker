# Overview

HttpMocker is designed around writing a controller like syntax and then directly translating it into an HttpMessageHandler to use as a data provider for tests.

> Idea was original but implementation wise I experimented with using chatgpt while on the go and then using the conversations to build out the project once home.

## Usage

Usage is very simple 

Write a class to that handles works as a controller to handle mock requests. You use the same attributes you would use for making a real controller like
- Route. Unlike normal route syntax this one takes a full url so you can use one controller to mock multiple different APIs
- HttpGet, HttpPost etc
- FromBody, FromQuery, FromHeader and FromRoute. **Note in this tool they must be added to all parameters to work it wont work it out on param name apart from route parameters but better to be explicit**
- Support for returning IActionResults but you can just return an object directly if its easier for testing
- Support for async handlers eg `async Task<IActionResult>`
- Support for static handlers or instanced ones if you want to pass other mock information to the controller

EG
```C#
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
```

You then pass this to the mocker and it wraps it in a HttpMessageHandler you can use to create HttpClients that will use your mock rather than real calls

```C#
var handler = Mocker.Create<DemoController>();
or
var handler = Mocker.Create(new DemoController(/*Other stuff to use in the controller routes to setup your mocks*/));


var client = new HttpClient(handler)
{
    BaseAddress = new Uri("http://service-test"),
};

client.DefaultRequestHeaders.Add("x-header", "foo");

var result = await client.PostAsJsonAsync("/api/test/hhh?queryParam=win", new TestBody
{
    HelloWorld = 6
});
```
