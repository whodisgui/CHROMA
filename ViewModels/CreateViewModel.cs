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

public class CreateViewModel : BaseViewModel
{
	// ==== INPUT HERE ==========================================

	string _baseHex = "#E96841"; //Sample Starter Color Code
	Color _baseColor = Colors.Orange;
	string _statusMessage = string.Empty;

    public string BaseHex
    {
        get => _baseHex;
        set
        {
			if (SetProperty(ref _baseHex, value, nameof(BaseHex)))
            {
				OnPropertyChanged(nameof(BaseHexPreview));
				UpdateBaseFromHex();
			}
		}
    }

    public string BaseHexPreview => $"Current: {BaseHex}";
    
    public Color BaseColor
    {
        get => _baseColor;
        private set => SetProperty(ref _baseColor, value, nameof(BaseColor));
    }

	public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value, nameof(StatusMessage));
    }


	// ==== SCHEME SELECTION ====================================

	public ObservableCollection<string> Schemes { get; } =
        new(new[]{
			"Monochromatic",
            "Complementary",
			"Split-Complementary",
            "Analogous",
            "Triadic",
            "Tetradic",
        });

	string _selectedScheme = "Monochromatic"; //Sample Starter Chosen Scheme

    public string SelectedScheme
    {
		get => _selectedScheme;
        set
        {
			if (SetProperty(ref _selectedScheme, value, nameof(SelectedScheme)))
            {
                GeneratePalette();
            }
		}
	}


	// ==== GENERATED PALETTE + EXPORT ==========================

	public ObservableCollection<ColorSlotViewModel> Palette { get; } = new();

	string _exportJson = string.Empty;
    public string ExportJson
    {
        get => _exportJson;
        private set => SetProperty(ref _exportJson, value, nameof(ExportJson));
    }

    // Simple 60/30/10 suggestion based on palette size (FR3/FR4 hook). :contentReference[oaicite:9]{index=9}
    public double PrimaryRatio => 0.6;
    public double SecondaryRatio => 0.3;
    public double AccentRatio => 0.1;


	// ==== COMMANDS ============================================

	public ICommand GenerateCommand => new Command(GeneratePalette);
	public ICommand SaveCommand => new Command(Save);
	public ICommand ExportJsonCommand => new Command(ExportPaletteJson);


	// ==== CORE LOGIC ==========================================

    void UpdateBaseFromHex()
    {
		if (!ColorMathService.TryParseHex(BaseHex, out var color))
        {
            StatusMessage = "Invalid HEX Color. Expected format = #RRGGBB.";
            return;
        }

        StatusMessage = string.Empty;
        BaseColor = color;
		GeneratePalette();
	}

    void GeneratePalette()
    {
        if (!ColorMathService.TryParseHex(BaseHex, out var color))
        {
			// If hex is bad, just keep current palette and show message.
			StatusMessage = "Cannot generate palette – invalid base HEX.";
            return;
		}

        var baseHSL = ColorMathService.ToHSL(color);

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

		Palette.Clear();

        for (int i = 0; i < generated.Length; i++)
        {
            string label = generated.Length == 2 && i == 1
                ? "Complement"
                : $"Color {i + 1}";

            Palette.Add(new ColorSlotViewModel(label, generated[i]));
		}

        StatusMessage = $"Generated {Palette.Count} colors using {SelectedScheme}.";
		ExportJson = string.Empty; // reset previous export
	}

    void Save()
    {
		// Hook point for JSON file save later. For now, just acknowledge the action.
		StatusMessage = "Palette saved (stub - wire to JSON file save/load next).";
	}

    void ExportPaletteJson()
    {
		var hexList = Palette
			.Select(p => ColorMathService.ToHex(p.Color))
            .ToArray();

        ExportJson = JsonSerializer.Serialize(hexList, new JsonSerializerOptions
        {
            WriteIndented = true
        });

		StatusMessage = "Export JSON prepared. (Copy from the text box or wire to file-save next.)";
	}
}

// Minimal base classes
public class BaseViewModel : ObservableObject { }
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