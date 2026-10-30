using HealthSys_20260428_v2.csproj.Data;
using HealthSys_20260428_v2.csproj.Services;

var builder = WebApplication.CreateBuilder(args);
// Ship the INAI settings with the application so moving servers preserves the connection.
builder.Configuration.AddJsonFile("inai-settings.json", optional: true, reloadOnChange: true);
builder.Services.Configure<InaiOptions>(builder.Configuration.GetSection("Inai"));
builder.Services.AddSingleton<InaiChatService>();
builder.Services.AddSingleton<IInaiLogService, DisabledInaiLogService>();
builder.Services.AddControllersWithViews();
builder.Services.AddAntiforgery(options => options.HeaderName = "RequestVerificationToken");
builder.Services.AddMemoryCache();
builder.Services.AddHttpClient();
builder.Services.AddHttpClient<IOpdOrderService, OpdOrderService>();
builder.Services.AddHttpClient<RegHospitalApiClient>(client =>
{
    client.BaseAddress = new Uri(RegHospitalApiClient.ProductionBaseUrl);
});
builder.Services.AddScoped<IAppointmentRegistrationService, AppointmentRegistrationService>();
builder.Services.AddScoped<IOPDBasicTbl, OPDBasicTbl>();

var app = builder.Build();

app.UseExceptionHandler("/Home/Error");
app.UseStatusCodePagesWithReExecute("/Home/Error/{0}");
app.UseStaticFiles();
app.UseRouting();

app.MapControllerRoute(

    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();


