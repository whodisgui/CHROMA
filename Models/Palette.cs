using System;
using SQLite;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CHROMA.Models;

/* ===SUMMARY===
 * A saved palette generated from CreatePage
 * persisted to the local SQLite database
 */
public class Palette
{

	[PrimaryKey, AutoIncrement]
	public int PaletteId { get; set; }

	// Optional user-facing name for this palette
	[MaxLength(100)]
	public string Name { get; set; } = string.Empty;

	// Base color as hex (e.g. "#FFAA00")
	[NotNull]
	public string BaseHex { get; set; } = string.Empty;

	// Harmony scheme name, e.g. "Complementary"
	[NotNull]
	public string Scheme { get; set; } = string.Empty;

}