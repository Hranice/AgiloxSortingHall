using AgiloxSortingHall.Data;
using AgiloxSortingHall.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace AgiloxSortingHall.Pages
{
    public class IndexModel : PageModel
    {
        private readonly ILogger<IndexModel> _logger;
        private readonly AppDbContext _db;

        public IndexModel(ILogger<IndexModel> logger, AppDbContext db)
        {
            _logger = logger;
            _db = db;
        }

        public List<WorkTable> Tables { get; set; } = new();

        public async Task OnGetAsync()
        {
            Tables = await _db.WorkTables.OrderBy(t => t.Id).ToListAsync();
        }
    }
}
