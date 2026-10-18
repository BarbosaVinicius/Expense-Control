using ExpenseControl.Core;
using ExpenseControl.Infrastructure;
using ExpenseControl.Web.Authentication;
using ExpenseControl.Web.Configuration;
using ExpenseControl.Web.Localization;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();

builder.Services.AddAuthorization();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddSessionAuthentication(builder.Configuration);

builder.Services.Configure<AppOptions>(builder.Configuration.GetSection(AppOptions.SectionName));

// Texts and formatting in the user's language (pt / en / es).
builder.Services.AddSingleton<TranslationStore>();
builder.Services.AddScoped<LanguageState>();
builder.Services.AddScoped<Localizer>();
builder.Services.AddScoped<Formatter>();

builder.Services.AddCoreServices();
builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

app.Run();

// Exposes the entry point to integration tests (WebApplicationFactory<Program>).
public partial class Program;
