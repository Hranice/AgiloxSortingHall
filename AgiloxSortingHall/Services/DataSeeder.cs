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

    public class DataSeeder
    {
        private readonly AppDbContext _db;
        private readonly HallConfig _config;

        public DataSeeder(AppDbContext db, IOptions<HallConfig> config)
        {
            _db = db;
            _config = config.Value;
        }

        public async Task SeedAsync()
        {
            // Seed řad
            foreach (var rowCfg in _config.Rows)
            {
                var row = await _db.HallRows
                    .Include(r => r.Slots)
                    .FirstOrDefaultAsync(r => r.Name == rowCfg.Name);

                if (row == null)
                {
                    row = new HallRow
                    {
                        Name = rowCfg.Name,
                        ColorHex = rowCfg.ColorHex,
                        Capacity = rowCfg.Capacity
                    };

                    // Vytvoříme pozice 0..Capacity-1
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
            }

            // Seed stolů
            foreach (var tblCfg in _config.Tables)
            {
                if (!await _db.WorkTables.AnyAsync(t => t.Name == tblCfg.Name))
                {
                    _db.WorkTables.Add(new WorkTable { Name = tblCfg.Name });
                }
            }

            await _db.SaveChangesAsync();
        }
    }
}
