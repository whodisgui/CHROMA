using System;
using SQLite;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CHROMA.Models;

/* ===SUMMARY===
 * Persisted critique result for a palette
 */
public class CritiqueReport
{
	[PrimaryKey, AutoIncrement]
	public int Id { get; set; }

	// Foreign key to the criticized palette
	[Indexed]
	public int PaletteId { get; set; }

	public string Summary { get; set; } = string.Empty;
	public string HarmonySpacing { get; set; } = string.Empty;
	public string Contrast { get; set; } = string.Empty;
	public string DeltaE { get; set; } = string.Empty;
	public string Balance { get; set; } = string.Empty;

	public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
}