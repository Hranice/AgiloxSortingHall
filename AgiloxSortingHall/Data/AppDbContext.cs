using AgiloxSortingHall.Models;
using Microsoft.EntityFrameworkCore;

namespace AgiloxSortingHall.Data
{
    public class AppDbContext : DbContext
    {
        public DbSet<HallRow> HallRows => Set<HallRow>();
        public DbSet<PalletSlot> PalletSlots => Set<PalletSlot>();
        public DbSet<WorkTable> WorkTables => Set<WorkTable>();
        public DbSet<RowCall> RowCalls => Set<RowCall>();
        public DbSet<HallSettings> HallSettings { get; set; } = null!;


        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Unikátní kombinace (HallRowId, PositionIndex),
            // aby v rámci řady nebyly dvě pozice se stejným pořadím
            modelBuilder.Entity<PalletSlot>()
                .HasIndex(p => new { p.HallRowId, p.PositionIndex })
                .IsUnique();

            modelBuilder.Entity<HallSettings>().HasData(
               new HallSettings
               {
                   Id = 1,
                   RowSelectionStrategy = RowSelectionStrategy.MostFreePallets
               });
        }
    }
}
