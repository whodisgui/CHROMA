using CHROMA.Services;
using Microsoft.Maui.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CHROMA.ViewModels;

/* ===FILE SUMMARY===
 * View‑model for a single palette entry on the Create page.
 * 
 * Holds:
 *   - A label (e.g. "Color 1", "Complement")
 *   - The color stored in HSL form (internal) plus a MAUI Color (for binding)
 *   - Adjustable saturation and lightness sliders
 * 
 * The hue component is intentionally fixed so users can only tweak S and L.
 * This class inherits from ObservableObject so that UI bindings update automatically
 * whenever properties change.
 */



public class ColorSlotViewModel : ObservableObject
{
	// Hue is fixed for this slot; only saturation/lightness can be tweaked by the user.
	readonly double _hue;

	double _saturation;
	double _lightness;
	Color _color;
	string _label;

	public ColorSlotViewModel(string label, HSLColor hsl)
	{
		_label = label;
		_hue = hsl.H;
		_saturation = hsl.S;
		_lightness = hsl.L;
		_color = ColorMathService.FromHSL(hsl);
	}

	// Human‑readable label shown in the UI (e.g., "Color 1" or "Complement").
	public string Label
	{
		get => _label;
		set => SetProperty(ref _label, value, nameof(Label));
	}

	/* ===SUMMARY===
    * Bound to a BoxView or Border.
    * 
    * Current display color for this slot.
    * Updating Saturation/Lightness will recalculate this value.
    */
	public Color Color
	{
		get => _color;
		private set => SetProperty(ref _color, value, nameof(Color));
	}

	/* ===SUMMARY===
    * S ∈ [0,1]. Only this + Lightness are tweakable by user.
    * 
    * Saturation slider backing field. Whenever this changes (and is clamped into [0,1]),
    * UpdateColor() is called to keep the UI swatch in sync.
    */
	public double Saturation
	{
		get => _saturation;
		set
		{
			if (SetProperty(ref _saturation, Clamp01(value), nameof(Saturation)))
				UpdateColor();
		}
	}

	/* ===SUMMARY===
    * L ∈ [0,1]. Only this + Saturation are tweakable by user.
    * 
    * Lightness slider backing field. Whenever this changes (and is clamped into [0,1]),
    * UpdateColor() is called to keep the UI swatch in sync.
    */
	public double Lightness
	{
		get => _lightness;
		set
		{
			if (SetProperty(ref _lightness, Clamp01(value), nameof(Lightness)))
				UpdateColor();
		}
	}

	// Returns the current HSL representation for this slot, combining the fixed hue with
	// the user-tunned saturation and lightness values.
	public HSLColor ToHSL()
	{
		return new HSLColor(_hue, _saturation, _lightness);
	}

	void UpdateColor()
	{
		// NOTE: hue is intentionally not exposed or changed here.
		// This enforces the "keep hue, tweak only S/L" rule.
		Color = ColorMathService.FromHSL(new HSLColor(_hue, _saturation, _lightness));
	}

	// Helper to constrain slider values into [0,1] to avoid invalid colors or math edge cases.
	static double Clamp01(double v) {
	    if (v < 0) {  return 0; }
		if (v > 1) { return 1; }
		return v;
	}
}