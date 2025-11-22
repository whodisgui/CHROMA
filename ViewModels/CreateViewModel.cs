using CHROMA.Services;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics.Arm;
using System.Text.Json;
using System.Windows.Input;

namespace CHROMA.ViewModels;

/* ===FILE SUMMARY===
 * Main view‑model for the "Create" tab/page in CHROMA.
 * 
 * Responsibilities:
 *   - Accept base color input in either HEX (#RRGGBB) or HSV form
 *   - Track which input mode is active and expose helper flags for XAML visibility
 *   - Let the user pick a harmony scheme (Monochromatic, Complementary, etc.)
 *   - Generate a palette (ObservableCollection<ColorSlotViewModel>) via HarmonyGenerator
 *   - Provide user‑facing messages for validation and palette status
 *   - Offer commands for Apply, Generate, Save (stub), Export JSON, and Reset
 * 
 * This class is the "glue" between the XAML UI and the pure color‑math services.
 */



public class CreateViewModel : BaseViewModel
{
	/* ==== INPUT HERE ==========================================
    *  Raw input fields + derived state for the base color used
    *  to generate palettes.
    */

	string _baseHex = string.Empty;
	Color _baseColor = Colors.Transparent;
    string _inputMessage = string.Empty;    // Feedback about the most recent input action (valid/invalid).
	string _paletteMessage = string.Empty;  // Feedback about the most recent palette generation/export.

	// HEX string the user types into the input box.
    // This is NOT validated until ApplyInput() is called.
	public string BaseHex
    {
        get => _baseHex;
        set
        {
			if (SetProperty(ref _baseHex, value, nameof(BaseHex)))
			{
                // Keep the preview label up to date as the user types.
				OnPropertyChanged(nameof(BaseHexPreview));
			}
		}
    }

	// Small text preview that simply echoes the current HEX input.
	public string BaseHexPreview => $"Current: {BaseHex}";

	// Canonical base color for the current palette (in MAUI Color form).
	// This is set only after input has been validated and applied.
	public Color BaseColor
    {
        get => _baseColor;
        private set => SetProperty(ref _baseColor, value, nameof(BaseColor));
    }

	// Message shown near the input controls (validation errors, success messages, etc.).
	public string InputMessage
    {
        get => _inputMessage;
        private set => SetProperty(ref _inputMessage, value, nameof(InputMessage));
    }

	// Message shown near the palette section (e.g., errors when generating, export status).
	public string PaletteMessage
    {
        get => _paletteMessage;
        private set => SetProperty(ref _paletteMessage, value, nameof(PaletteMessage));
    }


	/* ==== INPUT HERE (Name / Hex / HSV / HSL) =============================
	*  Mode selection allows the user to choose between HEX and HSV entry styles.
	*  Once a valid base color has been applied, the mode is locked until
	*  a new session is triggered (by pressing 'Reset').
	*/

	public ObservableCollection<string> InputModes { get; } =
        new(new[] { "Name", "HEX", "HSV", "HSL" });

    string _selectedInputMode = "Name";
    bool _inputModeLocked = false;

    public string SelectedInputMode
    {
        get => _selectedInputMode;
        set
        {
            // When an input mode is locked, ignore attempts to change it from the UI.
            if (_inputModeLocked && value != _selectedInputMode)
            {
                //Re-raise property so bindings re-sync the picker selection.
                OnPropertyChanged(nameof(SelectedInputMode));
                return;
            }

            if (SetProperty(ref _selectedInputMode, value, nameof(SelectedInputMode)))
            {
				// Notify XAML visibility bindings so the UI can swap between HEX and HSV sections.
				OnPropertyChanged(nameof(IsHexMode));
				OnPropertyChanged(nameof(IsHSVMode));
                OnPropertyChanged(nameof(IsHSLMode));
                OnPropertyChanged(nameof(IsNamedMode));
                OnPropertyChanged(nameof(CanChangeInputMode));

                // Swapping modes mid-session should clear any stale numeric/text values
                // but it should NOT unlock a previously locked mode.
				ClearInputState();
			}
        }
    }

	// Convenience bools for XAML visibility
	// Tracks whether ApplyInput() has successfully validated and stored a base color.
	public bool _hasValidBaseColor = false;

	public bool IsNamedMode => SelectedInputMode == "Name";
	public bool IsHexMode => SelectedInputMode == "HEX";
    public bool IsHSVMode => SelectedInputMode == "HSV";
    public bool IsHSLMode => SelectedInputMode == "HSL";

    // Expose whether the Picker should be interactive.
    public bool CanChangeInputMode => !_inputModeLocked;
	public bool IsInputModeLocked => _inputModeLocked;


	/* ==== HSV INPUT (when SelectedInputMode == "HSV") =========
    *  These properties are bound to HSV sliders / numeric inputs in the UI.
    */
	double _hsvHue;
    double _hsvSaturation = 100; // user-facing % 0-100
    double _hsvValue = 100;      // user-facing % 0-100

	// Hue in degrees (0–360). The UI is responsible for constraining to a sensible range.
	public double HSV_Hue
    {
        get => _hsvHue;
        set => SetProperty(ref _hsvHue, value, nameof(HSV_Hue));
    }

	// Saturation as a percentage (0–100) from the user's perspective.
	public double HSV_Saturation
	{
		get => _hsvSaturation;
		set => SetProperty(ref _hsvSaturation, value, nameof(HSV_Saturation));
	}

	// Value (brightness) as a percentage (0–100).
	public double HSV_Value
	{
		get => _hsvValue;
		set => SetProperty(ref _hsvValue, value, nameof(HSV_Value));
	}


    /* ==== HSL INPUT (when SelectedInputMode == "HSL") =========
    *  Similar shape to the HSV fields but uses Hue / Saturation / Lightness.
    *  Saturationn and Lightness are exposed as 0-100 percentages for the UI.
    */
    double _hslHue;
    double _hslSaturation = 100;
    double _hslLightness = 100;

    public double HSL_Hue
    {
        get => _hslHue;
        set => SetProperty(ref _hslHue, value, nameof(HSL_Hue));
    }

	public double HSL_Saturation
    {
        get => _hslSaturation;
        set => SetProperty(ref _hslSaturation, value, nameof(HSL_Saturation));
    }

    public double HSL_Lightness
    {
        get => _hslLightness;
        set => SetProperty(ref _hslLightness, value, nameof(HSL_Lightness));
    }


    /* ==== Name INPUT (when SelectedInputMode == "Name") =======
    *  Allows user to input standard color name (e.g., "Red", "LightGray", etc.)
    */
    string _namedColorName = string.Empty;
    public string NamedColorName
    {
        get => _namedColorName;
        set => SetProperty(ref _namedColorName, value, nameof(NamedColorName));
    }



	/* ==== SCHEME SELECTION ====================================
	*  List of scheme names displayed in the picker/dropdown on the Create page.
	*/

	public ObservableCollection<string> Schemes { get; } =
        new(new[]{
			"Monochromatic",
            "Complementary",
			"Split-Complementary",
            "Analogous",
            "Triadic",
            "Tetradic",
        });

	// Currently selected harmony scheme label. Used to pick the matching HarmonyScheme enum.
	string _selectedScheme = "Monochromatic"; //Sample Starter Chosen Scheme

    public string SelectedScheme
    {
		get => _selectedScheme;
        set
        {
			if (SetProperty(ref _selectedScheme, value, nameof(SelectedScheme)))
            {
				// Whenever the user changes schemes, immediately recompute the palette
				// (assuming a valid base color is already set).
				GeneratePalette();
            }
		}
	}


	// ==== GENERATED PALETTE + EXPORT ==========================

	// The current set of generated palette slots. Bound to a UI list of swatches.
	public ObservableCollection<ColorSlotViewModel> Palette { get; } = new();

	// JSON export string representing the current palette as a list of hex codes.
	string _exportJson = string.Empty;
    public string ExportJson
    {
        get => _exportJson;
        private set => SetProperty(ref _exportJson, value, nameof(ExportJson));
    }

	/* ===SUMMARY===
    *  Simple 60/30/10 suggestion based on palette size
    *  These values are exposed so the visualization layer can show a proportional usage hint
    *  (e.g., primary color ~60%, secondary ~30%, accent ~10%).
    */
	public double PrimaryRatio => 0.6;
    public double SecondaryRatio => 0.3;
    public double AccentRatio => 0.1;


	/* ==== COMMANDS ============================================
	*  Commands are bound to buttons in XAML to trigger actions from the UI layer.
	*/

	public ICommand GenerateCommand => new Command(GeneratePalette);
	public ICommand SaveCommand => new Command(Save);
	public ICommand ExportJsonCommand => new Command(ExportPaletteJson);
    public ICommand ApplyInputCommand => new Command(ApplyInput);
    public ICommand ResetCommand => new Command(ResetInputs);


	/* ==== CORE LOGIC ==========================================
	*  Private helpers that implement the actual behavior of the commands / UI interactions.
	*/

	// Re-computes the base color from the BaseHex string and regenerates the palette.
	// Currently unused by commands in favor of ApplyInput(), but kept as a focused helper.
	void UpdateBaseFromHex()
    {
		if (!ColorMathService.TryParseHex(BaseHex, out var color))
        {
            InputMessage = "Invalid HEX Color. Expected format = '#RRGGBB'; values = 0-9 and A-F.";
            return;
        }

        InputMessage = string.Empty;
        BaseColor = color;
		GeneratePalette();
	}

	// Re-computes the base color from the HSV sliders and regenerates the palette.
	void UpdateBaseFromHSV()
    {
        var hsv = new HSVColor(
            HSV_Hue,
            HSV_Saturation / 100.0,
            HSV_Value / 100.0);
        // Interpret Saturation / Value as percentages 0-100

        var color = ColorMathService.FromHSV(hsv);

        InputMessage = string.Empty;
        BaseColor = color;
        GeneratePalette();
    }

	// Central routine that turns the current base color + selected scheme into a palette.
	// It respects the selected input mode (HEX/HSV) and validates input before generating.
	void GeneratePalette()
    {
        if (!_hasValidBaseColor)
        {
            PaletteMessage = "Please enter and apply a valid base color first.";
            return;
        }

        Color color;

        // Re-derive base color from current input controls
        // so that the user can tweak raw values and hit "generate" again.
        if (IsHexMode)
        {
            if (!ColorMathService.TryParseHex(BaseHex, out color))
            {
                PaletteMessage = "Cannot generate palette - invalid base HEX.";
                return;
            }
        }
        else if (IsHSVMode)
        {
            var hsv = new HSVColor(
                HSV_Hue,
                HSV_Saturation / 100.0,
				HSV_Value / 100.0);

            color = ColorMathService.FromHSV(hsv);
        }
        else if (IsHSLMode)
        {
            var hsl = new HSLColor(
                HSL_Hue,
                HSL_Saturation / 100.0,
                HSL_Lightness / 100.0);

            color = ColorMathService.FromHSL(hsl);
        }
        else if (IsNamedMode)
        {
            if (!ColorMathService.TryParseNamedColor(NamedColorName, out color))
            {
                return;
            }
        }
        else
        {
            return;
        }

        var baseHSL = ColorMathService.ToHSL(color);

        // Map the selected scheme string to the internal HarmonyScheme enum.
        var scheme = SelectedScheme switch
        {
            "Monochromatic"       => HarmonyScheme.Monochromatic,
            "Complementary"       => HarmonyScheme.Complementary,
            "Split-Complementary" => HarmonyScheme.SplitComplementary,
            "Analogous"           => HarmonyScheme.Analogous,
            "Triadic"             => HarmonyScheme.Triadic,
            "Tetradic"            => HarmonyScheme.Tetradic,
            _                     => HarmonyScheme.Monochromatic
        };

		var generated = HarmonyGenerator.Generate(baseHSL, scheme);

		// Rebuild the palette collection so the UI updates its swatch list.
		Palette.Clear();

        for (int i = 0; i < generated.Length; i++)
        {
            string label = generated.Length == 2 && i == 1
                ? "Complement"
                : $"Color {i + 1}";

            Palette.Add(new ColorSlotViewModel(label, generated[i]));
		}

        PaletteMessage = $"Generated {Palette.Count} colors using {SelectedScheme}.";
		// Clear any previous export text so the user knows this palette hasn't been exported yet.
		ExportJson = string.Empty;
	}

    void Save()
    {
		// Hook point for JSON file save later. For now, just acknowledge the action.
		// When the persistence layer is implemented, this will write the current palette to disk.
		InputMessage = "Palette saved (stub - wire to JSON file save/load next).";
	}

	// Builds a JSON array of hex codes representing the current palette.
	// The resulting string is bound to a text box for copy‑paste or later file saving.
	void ExportPaletteJson()
    {
		var hexList = Palette
			.Select(p => ColorMathService.ToHex(p.Color))
            .ToArray();

        ExportJson = JsonSerializer.Serialize(hexList, new JsonSerializerOptions
        {
            WriteIndented = true
        });

		PaletteMessage = "Export JSON prepared. (Copy from the text box or wire to file-save next.)";
	}


	// Resets all user inputs, unlocks input mode, and clears the current palette/export state.
	void ResetInputs()
    {
        ClearInputState();

        _inputModeLocked = false;
        OnPropertyChanged(nameof(IsInputModeLocked));
        OnPropertyChanged(nameof(CanChangeInputMode));

        InputMessage = "Inputs reset. Enter a new color to generate a palette.";
    }

    // Clears all user-editable input fields and palette state,
    // but doesn't change current selected input mode or lock flags.
    void ClearInputState()
    {
        BaseHex = string.Empty;

        HSV_Hue = 0;
        HSV_Saturation = 0;
        HSV_Value = 0;

        HSL_Hue = 0;
        HSL_Saturation = 0;
        HSL_Lightness = 0;

        NamedColorName = string.Empty;

        BaseColor = Colors.Transparent;
        _hasValidBaseColor = false;

        Palette.Clear();
        ExportJson = string.Empty;
        PaletteMessage = string.Empty;
    }


	/* === SUMMARY ===
	*  Validates the current input (HEX / HSV / HSL / Name depending on mode)
    *  and stores it as BaseColor.
    *  Also flips the _hasValidBaseColor flag so GeneratePalette() can proceed
    *  in locks input mode.
	*/
	void ApplyInput()
	{
        // Treat "Enter" as: validate/apply current input and regenerate.
        Color color;

        if (IsNamedMode)
        {
            if (!ColorMathService.TryParseNamedColor(NamedColorName, out color))
            {
                InputMessage = "Unknown color name. Try something like Red, LightGray, or CornflowerBlue.";
                _hasValidBaseColor = false;
                return;
			}

            BaseColor = color;
            _hasValidBaseColor = true;
			InputMessage = $"Base color set from name \"{NamedColorName}\".";
		}
        else if (IsHexMode)
        {
            if (!ColorMathService.TryParseHex(BaseHex, out color))
            {
                InputMessage = "Invalid HEX Color. Expected format = '#RRGGBB'; values = 0-9 and A-F.";
                _hasValidBaseColor = false;
                return;
            }

            BaseColor = color;
            _hasValidBaseColor = true;
            InputMessage = "Base color set from HEX.";
        }
        else if (IsHSVMode)
        {
            var hsv = new HSVColor(
                HSV_Hue,
                HSV_Saturation / 100.0,
                HSV_Value / 100.0);

            // Range checks (e.g. 0-360, 0-100) handled by UI or clamped in color math.
            BaseColor = ColorMathService.FromHSV(hsv);
            _hasValidBaseColor = true;
            InputMessage = "Base color set from HSV.";
        }
        else if (IsHSLMode)
        {
            var hsl = new HSLColor(
                HSL_Hue,
                HSL_Saturation / 100.0,
                HSL_Lightness / 100.0);

            BaseColor = ColorMathService.FromHSL(hsl);
            _hasValidBaseColor = true;
            InputMessage = "Base color set from HSL.";
        }
        else
        {
            _hasValidBaseColor = false;
            InputMessage = "Unsupported input type.";
            return;
        }

        // Once base color is successfully applied, input mode is locked until explicit reset.
        _inputModeLocked = true;
        OnPropertyChanged(nameof(IsInputModeLocked));
		OnPropertyChanged(nameof(CanChangeInputMode));

        // When input changes, palette message no longer applies.
        PaletteMessage = string.Empty;
	}
}

// Minimal base classes
// BaseViewModel exists so additional shared behavior can be added later if needed.
public class BaseViewModel : ObservableObject { }

// Lightweight implementation of INotifyPropertyChanged for MVVM binding.
// SetProperty propagates changes to the UI whenever a property value actually changes.
public class ObservableObject : System.ComponentModel.INotifyPropertyChanged
{
    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
	protected bool SetProperty<T>(ref T storage, T value, string? propertyName = null)
	{
		if (EqualityComparer<T>.Default.Equals(storage, value)) return false;
		storage = value;
		OnPropertyChanged(propertyName);
		return true;
    }

	protected void OnPropertyChanged(string? propertyName)
    {
		PropertyChanged?.Invoke(
            this,
			new System.ComponentModel.PropertyChangedEventArgs(propertyName ?? string.Empty));
	}
}