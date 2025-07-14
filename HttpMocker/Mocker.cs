using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using System.Net;
using System.Reflection;
using System.Text;
using System.Text.Json;

namespace HttpMocker;

public class Mocker : HttpMessageHandler
{
    private readonly Type _controllerType;
    private readonly object? _controller;
    private readonly MethodInfo[] _methods;

    private Mocker(Type type, object? instance)
    {
        _controllerType = type;
        _controller = instance;
        _methods = _controller is null ?
            _controllerType.GetMethods(BindingFlags.Public | BindingFlags.Static) :
            _controllerType.GetMethods(BindingFlags.Public | BindingFlags.Instance);
    }

    public static HttpMessageHandler Create<T>(T instance)
        where T : class
    {
        return new Mocker(typeof(T), instance);
    }

    public static HttpMessageHandler Create<T>()
    {
        return new Mocker(typeof(T), null);
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, System.Threading.CancellationToken cancellationToken)
    {
        Dictionary<string, object> routeOarameters = new Dictionary<string, object>();
        MethodInfo? matchedMethod = FindRoute(request.RequestUri!, request.Method, routeOarameters);
        
        if (matchedMethod == null)
        {
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        }

        var parameters = matchedMethod.GetParameters();
        object?[] args = new object?[parameters.Length];

        var queryParams = System.Web.HttpUtility.ParseQueryString(request.RequestUri.Query);

        for (int i = 0; i < parameters.Length; i++)
        {
            var param = parameters[i];
            object? value = null;

            bool fromQuery = param.GetCustomAttribute<FromQueryAttribute>() != null;
            bool fromBody = param.GetCustomAttribute<FromBodyAttribute>() != null;
            var fromHeader = param.GetCustomAttribute<FromHeaderAttribute>();

            if (fromHeader is not null)
            {
                value = request.Headers.TryGetValues(fromHeader.Name, out var values) ? values.FirstOrDefault() : null;
            }
            else if (fromQuery)
            {
                value = FromQuery(queryParams, param);
            }
            else if (fromBody)
            {
                value = await FromBody(param, request);
            }
            else if (routeOarameters.TryGetValue(param.Name!.ToLowerInvariant(), out var routeVal))
            {
                value = Convert.ChangeType(routeVal, param.ParameterType);
            }

            if (value == null && param.HasDefaultValue)
            {
                value = param.DefaultValue;
            }

            args[i] = value;
        }

        object? result = matchedMethod.Invoke(_controller, args);


        if (result is null)
        {
            return new HttpResponseMessage(HttpStatusCode.OK);
        }

        if (result is Task task)
        {
            await task;
            result = task.GetType().GetProperty("Result")?.GetValue(task);
        }

        if (result is null)
        {
            return new HttpResponseMessage(HttpStatusCode.OK);
        }

        return ConvertToHttpResponse(result);
    }

    private static async Task<object?> FromBody(ParameterInfo param, HttpRequestMessage requestMessage)
    {
        if (requestMessage.Content is null)
        {
            return null;
        }

        if (param.ParameterType == typeof(string))
        {
            return requestMessage.Content.ReadAsStringAsync();
        }
        else if (param.ParameterType == typeof(byte[]))
        {
            return requestMessage.Content.ReadAsByteArrayAsync();
        }

        var sstring = await requestMessage.Content.ReadAsStringAsync();

        return JsonSerializer.Deserialize(sstring, param.ParameterType);
    }

    private static object? FromQuery(System.Collections.Specialized.NameValueCollection queryParams, ParameterInfo param)
    {
        var val = queryParams[param.Name];
        return val is null ? null : Convert.ChangeType(val, param.ParameterType);
    }

    private MethodInfo? FindRoute(Uri requestPath, HttpMethod requestMethod, Dictionary<string, object> routeParams)
    {
        foreach (var method in _methods)
        {
            var routeAttr = method.GetCustomAttribute<RouteAttribute>();
            if (routeAttr is not null)
            {
                var httpMethodAttr = method.GetCustomAttribute<HttpMethodAttribute>(true);
                var httpMethod = httpMethodAttr?.HttpMethods.FirstOrDefault();

                if (httpMethod == requestMethod.Method 
                    && MatchRoute(new Uri(routeAttr.Template), requestPath, routeParams))
                {
                    return method;
                }
            }
        }

        return null;
    }

    private HttpResponseMessage ConvertToHttpResponse(object result)
    {
        if (result is IActionResult action)
        {
            return ConvertActionResult(action);
        }

        return JsonResponse(200, result);
    }

    private HttpResponseMessage ConvertActionResult(IActionResult result)
    {
        return result switch
        {
            ObjectResult obj => JsonResponse(obj.StatusCode, obj.Value),
            StatusCodeResult sr => new HttpResponseMessage((HttpStatusCode)sr.StatusCode),
            ContentResult cr => new HttpResponseMessage((HttpStatusCode)(cr.StatusCode ?? 200))
            {
                Content = new StringContent(cr.Content ?? "", Encoding.UTF8, cr.ContentType ?? "text/plain")
            },
            _ => JsonResponse(200, null)
        };
    }

    private HttpResponseMessage JsonResponse(int? code, object? obj)
    {
        var json = obj != null ? JsonSerializer.Serialize(obj) : "";
        return new HttpResponseMessage((HttpStatusCode)(code ?? 200))
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
    }

    private bool MatchRoute(Uri template, Uri path, Dictionary<string, object> parameters)
    {
        if (template.Host != path.Host) return false;

        var templateParts = template.AbsolutePath.Trim('/').ToLowerInvariant().Split('/');
        var pathParts = path.AbsolutePath.Trim('/').ToLowerInvariant().Split('/');

        if (templateParts.Length != pathParts.Length)
        {
            return false;
        }

        for (int i = 0; i < templateParts.Length; i++)
        {
            if (templateParts[i] == "*")
            {
                continue;
            }
            else if (templateParts[i].StartsWith("{") && templateParts[i].EndsWith("}"))
            {
                var key = templateParts[i].Trim('{', '}').ToLowerInvariant();
                parameters[key] = pathParts[i];
            }
            else if (!string.Equals(templateParts[i], pathParts[i], StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }
        return true;
    }
}