using System;
using SQLite;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CHROMA.Models;

/* ===SUMMARY===
 * Single color belonging to a palette (1-to-many)
 */
public class ColorItem
{

	[PrimaryKey, AutoIncrement]
	public int ColorItemId { get; set; }

	// Foreign key to the palette it belongs to
	[Indexed]
	public int PaletteId { get; set; }

	// User-readable label like "Base color" or "Complement"
	[MaxLength(50)]
	public string ColorLabel { get; set; } = string.Empty;

	// Order within palette (i.e. first or second or etc. ; 0-based)
	public int ColorOrder { get; set; }

	// Hex code of color
	[NotNull]
	public string ColorHex { get; set; } = string.Empty;

	// HSL values of color; useful for recreation in ViewModels
	public double ColorHue { get; set; }
	public double ColorSat { get; set; }
	public double ColorLight { get; set; }

}