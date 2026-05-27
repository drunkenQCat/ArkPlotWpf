using ArkPlot.Core.Services;
using ArkPlot.WebDemo.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();

builder.Services.AddSingleton<StoryService>();
builder.Services.AddScoped<VisionService>();
builder.Services.AddScoped<TtsService>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseStaticFiles();
app.UseRouting();

app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

// 图片代理：解决跨域/防盗链问题
app.MapGet("/api/image", async (HttpContext ctx) =>
{
    var url = ctx.Request.Query["url"].FirstOrDefault();
    if (string.IsNullOrEmpty(url))
    {
        ctx.Response.StatusCode = 400;
        return;
    }

    try
    {
        using var http = new HttpClient();
        http.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0");
        http.DefaultRequestHeaders.Referrer = new System.Uri("https://prts.wiki/");
        http.Timeout = TimeSpan.FromSeconds(30);

        var resp = await http.GetAsync(url);
        resp.EnsureSuccessStatusCode();

        ctx.Response.ContentType = resp.Content.Headers.ContentType?.ToString() ?? "image/png";
        ctx.Response.Headers.CacheControl = "public, max-age=86400";
        await resp.Content.CopyToAsync(ctx.Response.Body);
    }
    catch
    {
        ctx.Response.StatusCode = 502;
    }
});

app.Run();
