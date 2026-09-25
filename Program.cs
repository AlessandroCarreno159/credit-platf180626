using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using credit_platf.Data;
using credit_platf.Hubs;
using credit_platf.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(connectionString));
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.AddDefaultIdentity<IdentityUser>(options => options.SignIn.RequireConfirmedAccount = false)
    .AddRoles<IdentityRole>()
    .AddErrorDescriber<DescripcionErroresIdentity>()
    .AddEntityFrameworkStores<ApplicationDbContext>();
builder.Services.AddControllersWithViews();
builder.Services.AddSignalR();

// Redis (P4, obligatorio, sin fallback): sesion + cache distribuida.
var redisConn = builder.Configuration["Redis:ConnectionString"];
if (string.IsNullOrWhiteSpace(redisConn))
    throw new InvalidOperationException(
        "Falta Redis:ConnectionString. Configúrala con user-secrets (local) o variable de entorno Redis__ConnectionString.");
builder.Services.AddStackExchangeRedisCache(o =>
{
    o.Configuration = redisConn;
    o.InstanceName = builder.Configuration["Redis:InstanceName"] ?? "creditplatf:";
});
builder.Services.AddHttpContextAccessor();
builder.Services.AddSession(o =>
{
    o.IdleTimeout = TimeSpan.FromMinutes(20);
    o.Cookie.HttpOnly = true;
    o.Cookie.IsEssential = true;
});
builder.Services.AddScoped<CacheSolicitudes>();
builder.Services.AddHttpClient("piesocket");
builder.Services.AddScoped<PieSocketPublisher>();

// RabbitMQ CloudAMQP (P7): productor singleton + consumidor (el flag vive dentro del servicio).
builder.Services.AddSingleton<RabbitMqPublisher>();
builder.Services.AddHostedService<SolicitudNotificacionConsumer>();

var app = builder.Build();

// P8: tras el proxy TLS de Render, sin esto UseHttpsRedirection entra en loop.
// Render no publica lista de IPs: se acepta cualquier proxy (1 sola instancia propia).
var forwarded = new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
};
forwarded.KnownNetworks.Clear();
forwarded.KnownProxies.Clear();
app.UseForwardedHeaders(forwarded);

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

app.UseAuthentication();
app.UseSession();
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.MapHub<SolicitudesHub>("/hubs/solicitudes");

// P8: health check de Render.
app.MapGet("/health", () => Results.Ok("ok"));

app.MapRazorPages()
   .WithStaticAssets();

// P8: en Render (Free, SQLite efimero) migra + seed idempotente en cada arranque.
// En local se usa igual con APLICAR_MIGRACIONES=true o el flag clasico SEED/--seed.
if (builder.Configuration.GetValue<bool>("APLICAR_MIGRACIONES")
    || builder.Configuration.GetValue<bool>("SEED")
    || args.Contains("--seed"))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    await db.Database.MigrateAsync();
    await SeedData.InitializeAsync(scope.ServiceProvider);
}

app.Run();
