var builder = WebApplication.CreateBuilder(args);

// Loopback only, fixed in code (Constitution III); configurable binding arrives with spec 002.
// Dropping the config-backed endpoint loader and listening explicitly means no environment
// variable, command-line argument or appsettings.json can add or widen a listener.
builder.WebHost.PreferHostingUrls(false);
builder.WebHost.ConfigureKestrel(options =>
{
    options.Configure(new ConfigurationBuilder().Build());
    options.ListenLocalhost(8484);
});

builder.Services.AddHealthChecks();

var app = builder.Build();

app.MapHealthChecks("/health");

app.Run();
