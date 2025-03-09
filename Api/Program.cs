using Api;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Http;
using Serilog;

var builder = WebApplication.CreateBuilder(args);
var configuration = builder.Configuration;
var services = builder.Services;

services.AddControllers();
services.AddEndpointsApiExplorer();
services.AddSwaggerGen();

services.ConfigureAll<HttpClientFactoryOptions>(options =>
{
    options.HttpMessageHandlerBuilderActions.Add(builder =>
    {
        builder.AdditionalHandlers.Add(builder.Services.GetRequiredService<PerformanceRequestHandler>());
    });
});
services.AddScoped<PerformanceRequestHandler>();
services.AddHttpClient();
services.AddSerilog();
services.AddScoped(sp => new HtmlRenderer(sp, sp.GetRequiredService<ILoggerFactory>()));

var serilogConfiguration = configuration.GetSection("Serilog");
Log.Logger = new LoggerConfiguration()
    .Enrich.FromLogContext()
    .Enrich.WithProcessName()
    .Enrich.WithProcessId()
    .Enrich.WithThreadName()
    .Enrich.WithThreadId()
    .Enrich.WithMemoryUsage()
    .Enrich.WithProperty("ContainerId", Environment.GetEnvironmentVariable("HOSTNAME"))
    .WriteTo.Console()
    .WriteTo.Seq(serilogConfiguration.GetSection("Seq").GetValue<string>("Url"))
    .CreateBootstrapLogger();

try
{
    var app = builder.Build();
    app.UseSwagger();
    app.UseSwaggerUI();
    app.UseSerilogRequestLogging();
    app.UseAuthorization();
    app.MapControllers();

    app.MapGet("/api/report", async ([FromServices] HtmlRenderer htmlRenderer) =>
    {
        var html = await htmlRenderer.Dispatcher.InvokeAsync(async () =>
        {
            var dictionary = new Dictionary<string, object?>
            {
                { "Message", "This is a message in the report" }
            };
            var parameters = ParameterView.FromDictionary(dictionary);
            var output = await htmlRenderer.RenderComponentAsync<Report>(parameters);
            return output.ToHtmlString();
        });

        return Results.Content(html, contentType: "text/html");
    });

    app.Run();
}
catch (Exception e)
{
    Log.Error(e, "Unexpected error occurred");
}
finally
{
    await Log.CloseAndFlushAsync();
}