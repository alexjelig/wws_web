using Microsoft.AspNetCore.Session;
using wws_web.Data;
using wws_web.Services;
using System.Diagnostics;
using Microsoft.Extensions.Logging;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddRazorPages();
builder.Services.AddDistributedMemoryCache();
builder.Services.AddMemoryCache();
builder.Services.AddHttpContextAccessor();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

builder.Services.AddScoped<LoginService>();
builder.Services.AddSingleton<IFileSettingsService, FileSettingsService>();
builder.Services.AddSingleton<SqliteDbHandler>();

// DeviceManager
builder.Services.AddSingleton<DeviceManager>(sp =>
{
    Debug.WriteLine("[Program] Registering DeviceManager...");
    var manager = new DeviceManager();
    Debug.WriteLine("[Program] DeviceManager successfully registered.");
    return manager;
});

// ScannerDevice (single hardware owner)
// NOTE: we do NOT start the device at startup here. Starting/opening the serial port
// must be done explicitly when a page or client needs it (via Acquire/Start).
builder.Services.AddSingleton<ScannerDevice>(sp =>
{
    var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
    var logger = loggerFactory.CreateLogger<ScannerDevice>();
    var deviceManager = sp.GetRequiredService<DeviceManager>();

    Debug.WriteLine("[Program] Registering ScannerDevice...");

    // Create scanner instance but DO NOT call Start() here to avoid grabbing the COM port
    // at application startup. Opening the port should be done on-demand (Acquire/Start).
    var scanner = new ScannerDevice("Scanner", @"C:\wws\scanner.json");

    // Register with the DeviceManager so pages can resolve it by name.
    // IMPORTANT: Ensure your DeviceManager.Register implementation does NOT call Start() automatically,
    // otherwise the port would still be opened at startup. DeviceManager.Register should only store the device.
    deviceManager.Register(scanner.Name, scanner);

    Debug.WriteLine("[Program] ScannerDevice registered (not started).");
    return scanner;
});

// Other devices/services (example)
// Do not start any serial/port devices here. Let pages or hubs acquire/start them when needed.
builder.Services.AddSingleton<BaykonWeightReaderService>(sp =>
{
    var weightReaderService = new BaykonWeightReaderService();
    sp.GetRequiredService<DeviceManager>().Register("BaykonWeightReader", weightReaderService);
    Debug.WriteLine("[Program] BaykonWeightReaderService registered (not started).");
    return weightReaderService;
});

var app = builder.Build();

// Resolve scanner to validate it was created (this does NOT start the device)
var scannerDevice = app.Services.GetRequiredService<ScannerDevice>();
if (scannerDevice != null)
{
    Debug.WriteLine("[Program] ScannerDevice instance available (not started at startup).");
}
else
{
    Debug.WriteLine("[Program] ERROR: ScannerDevice is NULL after initialization.");
}

// Init DB
var sqliteHandler = app.Services.GetRequiredService<SqliteDbHandler>();
sqliteHandler.InitializeDatabase();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseSession();
app.UseAuthorization();
app.MapRazorPages();

Debug.WriteLine("[Program] Going to execute app.Run()");
app.Run();
