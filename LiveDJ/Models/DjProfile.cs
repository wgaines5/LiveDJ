using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LiveDJ.Models;
public class DjProfile
{
    public string Uid { get; set; } = default!;
    public string Name { get; set; } = default!;
    public string Email { get; set; } = default!;
    public string? Phone { get; set; }
    public string? State { get; set; }
    public string? Address { get; set; }
    public string? PhotoUrl { get; set; }
    public string? BackgroundUrl { get; set; }
    public string? Bio { get; set; }
    public string[]? Genres { get; set; }
    public string? PortfolioUrl { get; set; }
}
