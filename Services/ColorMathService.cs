using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;
using Microsoft.Maui.Graphics;

namespace CHROMA.Services;

/* ===FILE SUMMARY===
 * Core color‑math helpers used by CHROMA.
 * Contains:
 *   - HSLColor / HSVColor lightweight structs used as internal representations
 *   - ColorMathService: conversions between HEX, RGB (Color), HSL, and HSV
 *   - HarmonyScheme / HarmonyGenerator: helpers for generating color harmony palettes
 * 
 * This file is intentionally UI‑agnostic and can be unit‑tested in isolation.
 */



/* ===SUMMARY===
* Simple HSL Color Struct for Internal Math
* Hue (H) in [0, 360),
* Saturation (S) and Lightness (L) in [0,1].
* 
* Immutable Representation of an HSL color used throughout the app's color math.
* Constructor automatically normalizes (or "clamps") channel values (H, S, and L)
* so callers can assume validity.
*/
public readonly struct HSLColor
{
	public double H { get; }  // H = Hue Value
    public double S { get; }  // S = Saturation Value
	public double L { get; }  // L = Lightness Value

	public HSLColor(double h, double s, double l)
	{
		H = NormalizeHue(h);
		S = Clamp01(s);
		L = Clamp01(l);
	}

	/* ===SUMMARY===
	* Wraps any hue into canonical [0,360) range (based on what degree they
	* sit on in the color wheel) so hue math can be done with simple arithmetic.
	*/
	internal static double NormalizeHue(double h)
	{
		h %= 360.0;
		if (h < 0) { h += 360.0; }
		return h;
	}

	/* ===SUMMARY===
	* Saturation (S) represents color intensity; Lightness (L) represents
	* how light or dark it is (both range from 0-100).
	* This keeps them within that range (reduced to [0,1]).
	*/
	internal static double Clamp01(double v)
	{
		if (v < 0) { return 0; }
		else if (v > 1) { return 1; }
		else { return v; }
	}

	// Convenience helper to reuse the same hue while tweaking S and L.
	public HSLColor WithSL(double s, double l)
	{
		return new HSLColor(H, s, l);
	}

	public override string ToString()
	{
		return $"H:{H:0},{S:0.00},{L:0.00}";
	}
}


/* ===SUMMARY===
* Simple HSV Color Struct for Internal Math
* Hue (H) in [0, 360),
* Saturation (S) and Value (V) in [0,1].
* 
* Similar to HSLColor, but for HSV; mainly for user-facing
* HSV input/preview logic.
*/
public readonly struct HSVColor
{
	public double H { get; }  // H = Hue Value
	public double S { get; }  // S = Saturation Value
	public double V { get; }  // V = Value Value

	public HSVColor(double h, double s, double v)
	{
		H = HSLColor.NormalizeHue(h);
		S = HSLColor.Clamp01(s);
		V = HSLColor.Clamp01(v);
	}

	public override string ToString()
	{
		return $"H:{H:0},{S:0.00},{V:0.00}";
	}
}

/* ===SUMMARY===
 * Static helper for all color conversions used by the Create / Critique flows.
 * Everything here is pure math with no UI or persistence dependencies.
 */
public static class ColorMathService
{
	/* ===SUMMARY===
	* Parses a Hex string (RRGGBB or #RRGGBB) to a color.
	* If invalid, returns false.
	* 
	* Returns a MAUI Color from a hex string, or false if the string is malformed.
	* This is the main entry point for user-supplied HEX in the Create page.
	*/
	public static bool TryParseHex(string? hex, out Color color)
	{
		color = Colors.Transparent;
		if (string.IsNullOrWhiteSpace(hex)) { return false; }

		hex = hex.Trim();
		if (hex.StartsWith("#")) { hex = hex[1..]; }
		if (hex.Length != 6) { return false; }

		if (!int.TryParse(hex[..2], System.Globalization.NumberStyles.HexNumber, null, out var rByte) ||            // If parsing Red Byte
		   !int.TryParse(hex.Substring(2, 2), System.Globalization.NumberStyles.HexNumber, null, out var gByte) ||  // Green Byte
		   !int.TryParse(hex.Substring(4, 2), System.Globalization.NumberStyles.HexNumber, null, out var bByte))    // or Blue Byte fails
		{
			return false;
		}

		color = new Color((float)(rByte / 255.0), (float)(gByte / 255.0), (float)(bByte / 255.0));
		return true;
	}


	/* ===SUMMARY===
	* Attempts to resolve a standard named color (e.g. "Red", "LightGray") to a MAUI Color.
	* 
	* Names map to properties on Microsoft.Maui.Graphics.Colors. library
	* Returns false if no matching color is found.
	*/
	public static bool TryParseNamedColor(string? name, out Color color)
	{
		color = Colors.Transparent;
		if (string.IsNullOrWhiteSpace(name)) { return false; }

		// Normalizes string by stripping spaces & dashes
		// so e.g. "light gray", "Light-Gray" → "lightgray"
		var normalized = new string(
			name.Trim()
				.Where(c => !char.IsWhiteSpace(c) && c != '-' && c != '_')
			    .ToArray()
		);

		/* ===SUMMARY===
		*  Colors defines fields, not properties:
		*  MAUI's color library: "148 public static readonly fields for common colors"
		*  So GetFields must be utilized.
		*/
		var field = typeof(Colors)
			.GetFields(BindingFlags.Public | BindingFlags.Static)
			.FirstOrDefault(f =>
				string.Equals(f.Name, normalized, StringComparison.OrdinalIgnoreCase));

		if (field?.GetValue(null) is Color found)
		{
			color = found;
			return true;
		}

		return false;
	}


	// Translates a Color to a #RRGGBB hex string (no alpha).
	// Used for exporting palettes and for displaying swatches as hex codes.
	public static string ToHex(Color color)
	{
		var r = (int)Math.Round(color.Red * 255);
		var g = (int)Math.Round(color.Green * 255);
		var b =	(int)Math.Round(color.Blue * 255);
		return $"#{r:X2}{g:X2}{b:X2}";
	}

	/* ===SUMMARY===
	* Converts RGB Color to HSL (Standard Definition)
	* 
	* Converts from MAUI's RGB-based Color into the internal HSL representation.
	* This is the main bridge for "artist-friendly" hue/saturation/lightness logic.
	*/
	public static HSLColor ToHSL(Color color)
	{
		double r = color.Red;
        double g = color.Green;
        double b = color.Blue;

		double max = Math.Max(r, Math.Max(g, b));
		double min = Math.Min(r, Math.Min(g, b));
		double c = max - min;
		double l = (max + min) / 2.0;
		double h;
		double s;

		if (c == 0)
		{
			h = 0;
			s = 0;
		}
		else
		{
			if (max == r)
				h = 60.0 * (((g - b) / c) % 6.0);
			else if (max == g)
				h = 60.0 * (((b - r) / c) + 2.0);
			else // max == b
				h = 60.0 * (((r - g) / c) + 4.0);

			if (h < 0) h += 360.0;

			s = c / (1 - Math.Abs(2 * l - 1));
		}

		return new HSLColor(h, s, l);
	}

	/* ===SUMMARY===
    * Converts HSL to RGB Color using standard hexcone math.
    * 
    * Inverse of ToHSLColor: takes a normalize HSL color and
    * produces a displayable MAUI color.
    */
	public static Color FromHSL(HSLColor hsl)
	{

		double h = HSLColor.NormalizeHue(hsl.H);
		double s = HSLColor.Clamp01(hsl.S);
		double l = HSLColor.Clamp01(hsl.L);

		double c = (1 - Math.Abs(2 * l - 1)) * s;
		double hPrime = h / 60.0;
        double x = c * (1 - Math.Abs(hPrime % 2 - 1));

		double r1, g1, b1;
		if (hPrime < 1)      { (r1, g1, b1) = (c, x, 0); }
		else if (hPrime < 2) { (r1, g1, b1) = (x, c, 0); }
		else if (hPrime < 3) { (r1, g1, b1) = (0, c, x); }
		else if (hPrime < 4) { (r1, g1, b1) = (0, x, c); }
		else if (hPrime < 5) { (r1, g1, b1) = (x, 0, c); }
		else                 { (r1, g1, b1) = (c, 0, x); }

		double m = l - c / 2.0;
		double r = r1 + m;
		double g = g1 + m;
		double b = b1 + m;

		return new Color((float)r, (float)g, (float)b);
	}

	/* ===SUMMARY===
    * Converts RGB Color to HSV.
    * 
    * Basically converts a display color into HSV space;
    * mainly used for driving HSV sliders and input.
    */
	public static HSVColor ToHSV(Color color)
	{
		double r = color.Red;
		double g = color.Green;
		double b = color.Blue;

		double max = Math.Max(r, Math.Max(g, b));
		double min = Math.Min(r, Math.Min(g, b));
		double c = max - min;

		double h;
		double s;
		double v = max;

		if (c == 0) { h = 0; s = 0; }
		else
		{
			if (max == r)
				h = 60.0 * (((g - b) / c) % 6.0);
			else if (max == g)
				h = 60.0 * (((b - r) / c) + 2.0);
			else // max == b
				h = 60.0 * (((r - g) / c) + 4.0);
			if (h < 0) h += 360.0;

			s = c / v;
		}

		return new HSVColor(h, s, v);
	}

	/* ===SUMMARY===
     * Converts HSV to RGB Color.
     * 
     * Inverse of ToHSV: takes a hue/saturation/value triple and returns a displayable Color.
     */
	public static Color FromHSV(HSVColor hsv)
	{
		double h = HSLColor.NormalizeHue(hsv.H);
		double s = HSLColor.Clamp01(hsv.S);
		double v = HSLColor.Clamp01(hsv.V);

		double c = v * s;
		double hPrime = h / 60.0;
		double x = c * (1 - Math.Abs(hPrime % 2 - 1));

		double r1, g1, b1;
		if (hPrime < 1) { (r1, g1, b1) = (c, x, 0); }
		else if (hPrime < 2) { (r1, g1, b1) = (x, c, 0); }
		else if (hPrime < 3) { (r1, g1, b1) = (0, c, x); }
		else if (hPrime < 4) { (r1, g1, b1) = (0, x, c); }
		else if (hPrime < 5) { (r1, g1, b1) = (x, 0, c); }
		else                 { (r1, g1, b1) = (c, 0, x); }

		double m = v - c;
		double r = r1 + m;
		double g = g1 + m;
		double b = b1 + m;

		return new Color((float)r, (float)g, (float)b);
	}

	/* ===SUMMARY===
     * Converts HSL to HSV with same hue.
     * 
     * Basically converts an HSL color into HSV while preserving hue,
     * useful when users switch input modes.
     */
	public static HSVColor HSLToHSV(HSLColor hsl)
	{
		double l = hsl.L;
		double s_l = hsl.S;

		double v = l + s_l * Math.Min(l, 1 - l);
		double s_v = v == 0 ? 0 : 2 * (1 - l / v);

		return new HSVColor(hsl.H, s_v, v);
	}

	/* ===SUMMARY===
     * Converts HSV to HSL with same hue.
     * 
     * Basically, converts an HSV color into HSL while, again,
     * preserving hue for mode switching.
     */
	public static HSLColor HSVToHSL(HSVColor hsv)
	{
		double v = hsv.V;
		double s_v = hsv.S;

		double l = v * (1 - s_v / 2);
		double s_l;

		if (l == 0 || l == 1)
			s_l = 0;
		else
			s_l = (v - l) / Math.Min(l, 1 - l);

		return new HSLColor(hsv.H, s_l, l);
	}
}


/* ===SUMMARY===
* The 6 Color Harmony Schemes that can be used on the CreatePage
* 
* These map directly to the options in the Create page's scheme picker.
*/
public enum HarmonyScheme
{
	Monochromatic,
	Complementary,
	SplitComplementary,
	Analogous,
	Triadic,
	Tetradic
}

/* ===SUMMARY===
* Generates complementing colors based on each Color Harmony Scheme
* 
* Given a base HSL color and a selected scheme, returns the derived HSL palette
* (still in HSL so the UI can later tweak saturation/lightness per slot).
*/
public static class HarmonyGenerator
{

	public static HSLColor[] Generate(HSLColor baseColor, HarmonyScheme scheme)
	{
		switch (scheme)
		{
			case HarmonyScheme.Monochromatic:
				// Same hue; vary S and L but keep them in a usable range
				// to avoid washed-out or overly dark swatches.
				return new[]
				{
					baseColor.WithSL(baseColor.S * 0.3, ClampMid(baseColor.L * 0.6)),
					baseColor,
					baseColor.WithSL(baseColor.S * 0.8, ClampMid(baseColor.L * 1.2))
				};

			case HarmonyScheme.Complementary:
				// Base hue and its direct opposite (180° apart) on the color wheel
				return new[]
				{
					baseColor,
					new HSLColor(baseColor.H + 180, baseColor.S, baseColor.L)
				};

			case HarmonyScheme.SplitComplementary:
				// Base hue plus two hues around its complement (±30° from 180°)
				return new[]
				{
					baseColor,
					new HSLColor(baseColor.H + 150, baseColor.S, baseColor.L),
					new HSLColor(baseColor.H + 210, baseColor.S, baseColor.L)
				};

			case HarmonyScheme.Analogous:
				// Neighboring hues around the base (±30°) for smooth transitions
				return new[]
				{
					baseColor,
					new HSLColor(baseColor.H + 30, baseColor.S, baseColor.L),
					new HSLColor(baseColor.H - 30, baseColor.S, baseColor.L)
				};

			case HarmonyScheme.Triadic:
				// Three hues equally spaced (120° apart) for balanced contrast
				return new[]
				{
					baseColor,
					new HSLColor(baseColor.H + 120, baseColor.S, baseColor.L),
					new HSLColor(baseColor.H + 240, baseColor.S, baseColor.L)
				};

			case HarmonyScheme.Tetradic:
				// Simple rectangle: base, base+90, base+180, base+270
				return new[]
				{
					baseColor,
					new HSLColor(baseColor.H + 90, baseColor.S, baseColor.L),
					new HSLColor(baseColor.H + 180, baseColor.S, baseColor.L),
					new HSLColor(baseColor.H + 270, baseColor.S, baseColor.L)
				};

			default:
				// Fallback: just return the base color if something unexpected slips through.
				return new[] { baseColor };
		}
	}

	// Clamps values into [0,1] but is conceptually used to keep lightness in a mid-usable range.
	static double ClampMid(double v)
	{
		if (v < 0) { v = 0; }
		else if (v > 1) { v = 1; }
		
		return v;
	}
}