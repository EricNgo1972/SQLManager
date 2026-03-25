using SQLManager.Components;
using SQLManager.Configuration;
using SQLManager.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddWindowsService();

builder.Services.AddSystemd();

builder.Services
    .AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddOptions<SqlServerCatalogOptions>()
    .Bind(builder.Configuration.GetSection(SqlServerCatalogOptions.SectionName))
    .ValidateDataAnnotations()
    .Validate(
        options => options.Servers.Count > 0 &&
                   options.Servers.All(server => !string.IsNullOrWhiteSpace(server.Key) &&
                                                 !string.IsNullOrWhiteSpace(server.ConnectionString)),
        "At least one SQL Server entry with a key and connection string is required.")
    .ValidateOnStart();

builder.Services.AddSingleton<ISqlServerCatalog, ConfigurationSqlServerCatalog>();
builder.Services.AddScoped<IDatabaseAdminService, DatabaseAdminService>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseDefaultFiles();
app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
