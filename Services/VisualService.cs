using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CHROMA.Models;

namespace CHROMA.Services;

/* ===FILE SUMMARY===
 * Helper methods for the Visual tab.
 * 
 * Responsibilities:
 *  - Ensure each color in a palette has a reasonable usage Proportion value.
 *  - Provide simple default 60/30/10‑style distributions for N colors.
 * 
 * Operates only on domain models (ColorItem) so it can be reused by
 * the Visual view‑models and the critique engine.
 */

public class VisualService
{
	/* ===SUMMARY===
	 * Ensures that each ColorItem has a non‑zero Proportion and normalizes
	 * the set so that the proportions sum to 1.0. If all proportions are zero,
	 * assigns a simple default distribution based on the color count.
	*/
	public IList<ColorItem> EnsureProportions(IList<ColorItem> colors)
	{
		if (colors == null || colors.Count == 0)
			return Array.Empty<ColorItem>();

		// If there is at least some non‑zero proportion, normalize.
		double total = colors.Sum(c => c.Proportion);

		if (total > 0.0)
		{
			foreach (var c in colors)
			{
				c.Proportion = c.Proportion / total;
			}

			return colors;
		}

		// Otherwise, assign a default pattern (60 / 30 / 10 style).
		var defaults = BuildDefaultFractions(colors.Count);
		for (int i = 0; i < colors.Count; i++)
		{
			colors[i].Proportion = defaults[i];
		}

		return colors;
	}

	/* ===SUMMARY===
	 * Builds a simple default usage distribution. For example:
	 *   1 color  → [1.0]
	 *   2 colors → [0.7, 0.3]
	 *   3 colors → [0.6, 0.3, 0.1]
	 *   4+       → [0.6, 0.2, remainder split evenly across the rest]
	 * The results always sum (approximately) to 1.0.
	*/
	public double[] BuildDefaultFractions(int count)
	{
		if (count <= 0)
			return Array.Empty<double>();

		if (count == 1)
			return new[] { 1.0 };

		if (count == 2)
			return new[] { 0.7, 0.3 };

		if (count == 3)
			return new[] { 0.6, 0.3, 0.1 };

		// For 4 or more colors: 60% primary, 20% secondary, remaining 20% split evenly.
		var result = new double[count];
		result[0] = 0.6;
		result[1] = 0.2;

		var remainingShare = 0.2;
		int tailCount = count - 2;
		double tail = remainingShare / tailCount;

		for (int i = 2; i < count; i++)
		{
			result[i] = tail;
		}

		return result;
	}
}