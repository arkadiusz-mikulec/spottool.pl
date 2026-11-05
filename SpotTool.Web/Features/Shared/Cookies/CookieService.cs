using System.Text.Json;
using Microsoft.JSInterop;
using SpotTool.Web.Domain.Types;

namespace SpotTool.Web.Features.Shared.Cookies;

public class CookieService(IJSRuntime _jSRuntime, IHttpContextAccessor _httpContextAccessor)
{
    private readonly string key = "userEmailAndOffset";
    // public async Task SetUserEmailAndOffsetCookieAsync(UserEmailAndTimeOffset data, int days)
    // {
    //     //UserEmailAndTimeOffset data = new (email, offset);
    //     string value = JsonSerializer.Serialize(data);
    //     await  _jSRuntime.InvokeVoidAsync("cookieFunctions.setCookie", key, value, days);
    // }
    public async Task SetUserEmailAndOffsetCookieAsync(string email, int days = 7)
    {
        var offset = await _jSRuntime.InvokeAsync<int>("getUserTimezoneOffsetInMinutes");
        UserEmailAndTimeOffset data = new (email.ToLower(), TimeSpan.FromMinutes(offset));

        string value = JsonSerializer.Serialize(data);
        await  _jSRuntime.InvokeVoidAsync("cookieFunctions.setCookie", key, value, days);
    }
    public async Task<UserEmailAndTimeOffset?> GetUserEmailAndOffsetCookieAsync()
    {
        string value = await _jSRuntime.InvokeAsync<string>("cookieFunctions.getCookie", key);
        if(string.IsNullOrEmpty(value))
            return null;
        return JsonSerializer.Deserialize<UserEmailAndTimeOffset>(value);
    }

//Nie potrzebne jezeli wyłączyliśmy prerender mode.
//Potrzebne tylko jezeli mamy prerender i chcemy pobrac cookie z serwera w fazie prerender.
// wowczas w OnInitialize mamy dostepe do HttpContext i mozemy pobierac cookie
    public async Task<UserEmailAndTimeOffset?> GetUserEmailAndOffsetCookieFromHttpContextAsync()
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext != null && httpContext.Request.Cookies.TryGetValue(key, out var value))
        {
            if(string.IsNullOrEmpty(value))
                return null; 
            return JsonSerializer.Deserialize<UserEmailAndTimeOffset>(value);
        }
        return null;
    }

    public async Task DeleteUserEmailAndOffsetCookieAsync()
    {
        await _jSRuntime.InvokeVoidAsync("cookieFunctions.setCookie", key, "", -1);
    }
}

public record UserEmailAndTimeOffset(string Email, TimeSpan Offset);