using AgiloxSortingHall.Data;
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
            // Řady (HallRows)
            foreach (var rowCfg in _config.Rows)
            {
                // pokusíme se najít existující řadu se stejným Name
                var row = await _db.HallRows
                    .Include(r => r.Slots)
                    .FirstOrDefaultAsync(r => r.Name == rowCfg.Name);

                if (row == null)
                {
                    // Řada neexistuje -> vytvoříme novou
                    row = new HallRow
                    {
                        Name = rowCfg.Name,
                        ColorHex = rowCfg.ColorHex,
                        Capacity = rowCfg.Capacity,
                        Slots = new List<PalletSlot>()
                    };

                    // vytvoříme sloty 0 .. Capacity-1
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
                    // Řada existuje -> aktualizujeme ji podle configu

                    // barva podle configu
                    row.ColorHex = rowCfg.ColorHex;

                    // pokud se změnila kapacita, upravíme i sloty
                    if (row.Capacity != rowCfg.Capacity)
                    {
                        var oldCapacity = row.Capacity;
                        var newCapacity = rowCfg.Capacity;
                        row.Capacity = newCapacity;

                        // aktuální počet slotů v DB
                        var currentSlots = row.Slots.ToList();

                        // zvýšení kapacity -> přidáme chybějící sloty
                        if (newCapacity > currentSlots.Count)
                        {
                            for (int i = currentSlots.Count; i < newCapacity; i++)
                            {
                                row.Slots.Add(new PalletSlot
                                {
                                    PositionIndex = i,
                                    State = PalletState.Empty
                                });
                            }
                        }
                        // snížení kapacity -> odstraníme sloty s PositionIndex >= newCapacity
                        else if (newCapacity < currentSlots.Count)
                        {
                            var toRemove = currentSlots
                                .Where(s => s.PositionIndex >= newCapacity)
                                .ToList();

                            _db.PalletSlots.RemoveRange(toRemove);
                        }
                    }
                }
            }

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
