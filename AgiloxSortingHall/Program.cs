using AgiloxSortingHall.Data;
using AgiloxSortingHall.Hubs;
using AgiloxSortingHall.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

//builder.WebHost.ConfigureKestrel(options =>
//{
//    // Naslouchat na všech IP (0.0.0.0) na portu 5044 – èisté HTTP
//    options.ListenAnyIP(5044);

//    options.ListenAnyIP(7002, listenOptions =>
//    {
//        listenOptions.UseHttps();
//    });
//});


// DbContext s SQLite
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.Configure<HallConfig>(
    builder.Configuration.GetSection("HallConfig"));
builder.Services.AddTransient<DataSeeder>();

// Add services to the container.
builder.Services.AddRazorPages();

builder.Services.AddSignalR();

var app = builder.Build();

// Migrace / vytvoøení DB pøi startu
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
    var seeder = scope.ServiceProvider.GetRequiredService<DataSeeder>();
    await seeder.SeedAsync();
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseRouting();

app.UseAuthorization();

app.MapStaticAssets();
app.MapRazorPages()
   .WithStaticAssets();

app.MapHub<HallHub>("/hallHub");

app.Run();
