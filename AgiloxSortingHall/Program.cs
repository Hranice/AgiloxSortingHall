using AgiloxSortingHall.Data;
using AgiloxSortingHall.Hubs;
using AgiloxSortingHall.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.UseUrls("http://0.0.0.0:5000");

// DbContext s SQLite
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.Configure<HallConfig>(
    builder.Configuration.GetSection("HallConfig"));
builder.Services.AddTransient<DataSeeder>();

var agiloxBaseUrl = builder.Configuration["Agilox:BaseUrl"]
                     ?? throw new Exception("Missing Agilox BaseUrl in configuration");

builder.Services.AddHttpClient("Agilox", client =>
{
    client.BaseAddress = new Uri(agiloxBaseUrl);
    client.DefaultRequestHeaders.Add("Accept", "application/json");
});


builder.Services.AddScoped<AgiloxService>();


// Add services to the container.
builder.Services.AddRazorPages();

builder.Services.AddSignalR();

builder.Services.AddControllers();

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

//app.UseHttpsRedirection();

app.UseRouting();

app.UseAuthorization();

app.MapControllers();

app.MapStaticAssets();
app.MapRazorPages()
   .WithStaticAssets();

app.MapHub<HallHub>("/hallHub");

app.Run();
