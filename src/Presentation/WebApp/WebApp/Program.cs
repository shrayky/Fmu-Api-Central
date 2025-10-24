var builder = WebApplication.CreateBuilder(args);

Console.WriteLine("Starting WebApp application...");

builder.WebHost.UseUrls("http://+:2580");

builder.Services.AddRazorPages();

if (OperatingSystem.IsWindows())
{
    builder.Host.UseWindowsService();
}

var app = builder.Build();

Console.WriteLine("Application built successfully");

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = prm =>
    {
        prm.Context.Response.Headers.Append("Cache-Control", "publc, max-age=3600");
    }
});

app.UseRouting();

app.UseAuthorization();

app.MapRazorPages();

Console.WriteLine("Starting server on http://+:2580");
Console.WriteLine("Press Ctrl+C to stop the server");

await app.RunAsync();
