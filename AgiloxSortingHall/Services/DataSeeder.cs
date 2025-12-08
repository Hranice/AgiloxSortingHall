using AgiloxSortingHall.Data;
using AgiloxSortingHall.Enums;
using AgiloxSortingHall.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AgiloxSortingHall.Services
{
    public record HallRowConfig(string Name, string ColorHex, int Capacity);
    public record WorkTableConfig(string Name);

    public class HallConfig
    {
        public List<HallRowConfig> Rows { get; set; } = new();
        public List<WorkTableConfig> Tables { get; set; } = new();
    }

    /// <summary>
    /// Třída umožňující inicializaci databáze
    /// podle konfigurace haly (řady a stoly).
    /// </summary>
    public class DataSeeder
    {
        private readonly AppDbContext _db;
        private readonly HallConfig _config;

        /// <summary>
        /// Inicializuje DataSeeder injektovaným AppDbContextem
        /// a konfiguračními daty haly.
        /// </summary>
        public DataSeeder(AppDbContext db, IOptions<HallConfig> config)
        {
            _db = db;
            _config = config.Value;
        }

        /// <summary>
        /// Naplní databázi výchozími daty:
        /// - vytvoří řady dle konfigurace, včetně všech slotů
        /// - vytvoří pracovní stoly dle konfigurace
        /// Metoda je idempotentní (opakované spuštění nic nezdvojí).
        /// </summary>
        public async Task SeedAsync()
        {
            foreach (var rowCfg in _config.Rows)
            {
                var row = await _db.HallRows
                    .Include(r => r.Slots)
                    .FirstOrDefaultAsync(r => r.Name == rowCfg.Name);

                if (row == null)
                {
                    // vytvoření nové řady
                    row = new HallRow
                    {
                        Name = rowCfg.Name,
                        ColorHex = rowCfg.ColorHex,
                        Capacity = rowCfg.Capacity
                    };

                    for (int i = 0; i < rowCfg.Capacity; i++)
                    {
                        row.Slots.Add(new PalletSlot
                        {
                            PositionIndex = i,
                            State = PalletState.Empty
                        });
                    }

                    _db.HallRows.Add(row);
                }
                else
                {
                    // aktualizace existující řady = synchronizace kapacity
                    row.ColorHex = rowCfg.ColorHex;
                    row.Capacity = rowCfg.Capacity;

                    // Odstranit sloty navíc
                    var extraSlots = row.Slots
                        .Where(s => s.PositionIndex >= rowCfg.Capacity)
                        .ToList();

                    if (extraSlots.Any())
                        _db.PalletSlots.RemoveRange(extraSlots);

                    // Přidat chybějící sloty
                    for (int i = 0; i < rowCfg.Capacity; i++)
                    {
                        if (!row.Slots.Any(s => s.PositionIndex == i))
                        {
                            row.Slots.Add(new PalletSlot
                            {
                                PositionIndex = i,
                                State = PalletState.Empty
                            });
                        }
                    }
                }
            }

            await _db.SaveChangesAsync();

            // Stoly (WorkTables)
            foreach (var tblCfg in _config.Tables)
            {
                var exists = await _db.WorkTables.AnyAsync(t => t.Name == tblCfg.Name);
                if (!exists)
                {
                    _db.WorkTables.Add(new WorkTable
                    {
                        Name = tblCfg.Name
                    });
                }
            }

            await _db.SaveChangesAsync();
        }

    }
}
