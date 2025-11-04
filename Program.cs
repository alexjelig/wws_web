using Microsoft.AspNetCore.Session;
using wws_web.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages();

// Caching required for session and useful for other services
builder.Services.AddDistributedMemoryCache();
builder.Services.AddMemoryCache();

// Register IHttpContextAccessor if you need to access HttpContext from services/pages
builder.Services.AddHttpContextAccessor();

// Register session services
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30); // Session timeout as needed
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

// Register your LoginService as scoped
builder.Services.AddScoped<LoginService>();

// Register file-based settings service (singleton - lightweight, thread-safe)
builder.Services.AddSingleton<IFileSettingsService, FileSettingsService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseSession(); // <-- Important: Use session before authorization

app.UseAuthorization();

app.MapRazorPages();

app.Run();
