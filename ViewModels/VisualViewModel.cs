using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using CHROMA.Data;
using CHROMA.Models;
using CHROMA.Services;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;

namespace CHROMA.ViewModels;

/* ===FILE SUMMARY===
 * View-models for the "Visual" tab.
 * 
 * VisualViewModel         : Top‑level VM used as the BindingContext of the Visual page.
 *                           - Loads saved palettes from the SQLite database.
 *                           - Tracks the currently selected palette.
 *                           - Delegates per‑color usage logic to VisualChartViewModel.
 *                           - Exposes commands for resetting and saving usage.
 * 
 * VisualChartViewModel    : Holds the per‑color segments used by the painter's bar,
 *                           60/30/10 layout preview, and other chart‑style visuals.
 * 
 * VisualImageViewModel    : Placeholder for future image/overlay features (FR9).
 * 
 * VisualColorSegment      : Small helper VM that wraps a ColorItem and exposes
 *                           UI‑friendly properties (Color, Percent, BarWidth).
 */

// Top‑level coordinator for the Visual page.
public class VisualViewModel : BaseViewModel
{
	private readonly ChromaDatabase _database;
	private readonly VisualService _visualService;

	public ObservableCollection<Palette> SavedPalettes { get; } = new();

	public VisualChartViewModel Chart { get; }
	public VisualImageViewModel Image { get; }

	public ICommand LoadPalettesCommand { get; }
	public ICommand ResetProportionsCommand { get; }
	public ICommand SaveProportionsCommand { get; }

	public VisualViewModel()
	{
		_database = ChromaDatabase.Instance;
		_visualService = new VisualService();

		Chart = new VisualChartViewModel(_visualService);
		Image = new VisualImageViewModel();

		LoadPalettesCommand = new Command(async () => await LoadPalettesAsync());
		ResetProportionsCommand = new Command(() => ResetProportions());
		SaveProportionsCommand = new Command(async () => await SaveProportionsAsync());

		// Kick off initial load so the picker is populated when the tab opens.
		_ = LoadPalettesAsync();
	}

	private Palette? _selectedPalette;
	public Palette? SelectedPalette
	{
		get => _selectedPalette;
		set
		{
			if (SetProperty(ref _selectedPalette, value, nameof(SelectedPalette)))
			{
				_ = LoadColorsForSelectedPaletteAsync();
			}
		}
	}

	private string _statusMessage = string.Empty;
	public string StatusMessage
	{
		get => _statusMessage;
		set => SetProperty(ref _statusMessage, value, nameof(StatusMessage));
	}

	private bool _isBusy;
	public bool IsBusy
	{
		get => _isBusy;
		set => SetProperty(ref _isBusy, value, nameof(IsBusy));
	}

	private bool _supportsClassic601010;
	public bool SupportsClassic601010
	{
		get => _supportsClassic601010;
		private set => SetProperty(ref _supportsClassic601010, value, nameof(SupportsClassic601010));
	}

	private async Task LoadPalettesAsync()
	{
		try
		{
			IsBusy = true;
			SavedPalettes.Clear();

			var all = await _database.GetPalettesAsync();
			foreach (var p in all)
			{
				SavedPalettes.Add(p);
			}

			SelectedPalette = SavedPalettes.FirstOrDefault();

			StatusMessage = SavedPalettes.Count == 0
				? "No palettes saved yet. Create one on the Create tab first."
				: "Pick a palette to visualize its usage.";
		}
		catch (Exception ex)
		{
			StatusMessage = "Error loading palettes. See debug output for details.";
			Debug.WriteLine(ex);
		}
		finally
		{
			IsBusy = false;
		}
	}

	private async Task LoadColorsForSelectedPaletteAsync()
	{
		if (SelectedPalette == null)
		{
			Chart.Clear();
			SupportsClassic601010 = false;
			StatusMessage = "Pick a palette to visualize its usage.";
			return;
		}

		try
		{
			IsBusy = true;

			var colors = await _database.GetColorsForPaletteAsync(SelectedPalette.Id);
			Chart.LoadFromColors(colors);

			SupportsClassic601010 = Chart.Segments.Count == 3;

			if (Chart.Segments.Count == 0)
			{
				StatusMessage = "Selected palette has no colors yet.";
			}
			else
			{
				StatusMessage = "Adjust usage sliders, then optionally save changes.";
			}
		}
		catch (Exception ex)
		{
			StatusMessage = "Error loading colors for the selected palette.";
			Debug.WriteLine(ex);
		}
		finally
		{
			IsBusy = false;
		}
	}

	private void ResetProportions()
	{
		if (Chart.Segments.Count == 0)
			return;

		Chart.ResetToDefaultProportions();
		StatusMessage = SupportsClassic601010
			? "Usage reset to classic 60 / 30 / 10 (60% / 30% / 10%) pattern."
			: "Usage reset to a balanced pattern. 60/30/10 layout works best with 3‑color palettes.";
	}

	private async Task SaveProportionsAsync()
	{
		if (SelectedPalette == null || Chart.Segments.Count == 0)
			return;

		try
		{
			IsBusy = true;

			var items = Chart.Segments
				.Select(seg => seg.ToColorItem(SelectedPalette.Id))
				.ToList();

			await _database.UpdateColorsForPaletteAsync(SelectedPalette.Id, items);

			StatusMessage = "Usage proportions saved for this palette.";
		}
		catch (Exception ex)
		{
			StatusMessage = "Error saving usage proportions.";
			Debug.WriteLine(ex);
		}
		finally
		{
			IsBusy = false;
		}
	}
}


// Handles the per‑color data used by charts and previews on the Visual tab.
public class VisualChartViewModel : BaseViewModel
{
	private readonly VisualService _visualService;

	public ObservableCollection<VisualColorSegment> Segments { get; } = new();

	public VisualChartViewModel(VisualService visualService)
	{
		_visualService = visualService;
	}

	public void Clear()
	{
		Segments.Clear();
		PrimarySegment = SecondarySegment = AccentSegment = null;
	}

	public void LoadFromColors(IList<ColorItem> colors)
	{
		Segments.Clear();

		if (colors == null || colors.Count == 0)
		{
			PrimarySegment = SecondarySegment = AccentSegment = null;
			return;
		}

		var normalized = _visualService.EnsureProportions(colors);

		foreach (var c in normalized)
		{
			Segments.Add(new VisualColorSegment(c));
		}

		UpdateRoleSegments();
	}

	public void ResetToDefaultProportions()
	{
		if (Segments.Count == 0)
			return;

		var defaults = _visualService.BuildDefaultFractions(Segments.Count);
		for (int i = 0; i < Segments.Count; i++)
		{
			Segments[i].Proportion = defaults[i];
		}

		UpdateRoleSegments();
	}

	private VisualColorSegment? _primarySegment;
	public VisualColorSegment? PrimarySegment
	{
		get => _primarySegment;
		private set => SetProperty(ref _primarySegment, value, nameof(PrimarySegment));
	}

	private VisualColorSegment? _secondarySegment;
	public VisualColorSegment? SecondarySegment
	{
		get => _secondarySegment;
		private set => SetProperty(ref _secondarySegment, value, nameof(SecondarySegment));
	}

	private VisualColorSegment? _accentSegment;
	public VisualColorSegment? AccentSegment
	{
		get => _accentSegment;
		private set => SetProperty(ref _accentSegment, value, nameof(AccentSegment));
	}

	private void UpdateRoleSegments()
	{
		if (Segments.Count == 0)
		{
			PrimarySegment = SecondarySegment = AccentSegment = null;
			return;
		}

		// Use the highest‑usage colors as primary/secondary/accent.
		var ordered = Segments
			.OrderByDescending(s => s.Proportion)
			.ToList();

		PrimarySegment = ordered.ElementAtOrDefault(0);
		SecondarySegment = ordered.ElementAtOrDefault(1);
		AccentSegment = ordered.ElementAtOrDefault(2);
	}
}


// Placeholder for future image / overlay features (FR9).
public class VisualImageViewModel : BaseViewModel
{
	private string _lastSavedImagePath = string.Empty;

	// For now we just expose a path where a generated preview might be stored.
	public string LastSavedImagePath
	{
		get => _lastSavedImagePath;
		set => SetProperty(ref _lastSavedImagePath, value, nameof(LastSavedImagePath));
	}
}

// Wraps a ColorItem and exposes UI‑friendly properties for the Visual page.
public class VisualColorSegment : ObservableObject
{
	public int ColorItemId { get; }
	public int PaletteId { get; }

	public string Label { get; }
	public int Order { get; }

	public string Hex { get; }

	// UI color used for swatches and bars.
	public Color Color { get; }

	// HSL values are kept so we can round‑trip back to ColorItem.
	public double H { get; }
	public double S { get; }
	public double L { get; }

	private double _proportion; // 0–1 fraction

	/* ===SUMMARY===
	 * Fraction of total usage (0–1). BarWidth and Percent are derived from this.
	 */
	public double Proportion
	{
		get => _proportion;
		set
		{
			if (SetProperty(ref _proportion, value, nameof(Proportion)))
			{
				OnPropertyChanged(nameof(Percent));
				OnPropertyChanged(nameof(BarWidth));
			}
		}
	}

	/* ===SUMMARY===
	 * Percentage 0–100, friendly for sliders and labels.
	 */
	public double Percent
	{
		get => _proportion * 100.0;
		set
		{
			var clamped = Math.Clamp(value, 0, 100);
			var fraction = clamped / 100.0;

			if (SetProperty(ref _proportion, fraction, nameof(Proportion)))
			{
				OnPropertyChanged(nameof(Percent));
				OnPropertyChanged(nameof(BarWidth));
			}
		}
	}

	/* ===SUMMARY===
	 * Width used for the painter's bar. We keep it simple: 100% = 300 units.
	 */
	public double BarWidth => Proportion * 300.0;

	public VisualColorSegment(ColorItem item)
	{
		ColorItemId = item.Id;
		PaletteId = item.PaletteId;
		Label = item.Label;
		Order = item.Order;
		Hex = item.Hex;
		H = item.H;
		S = item.S;
		L = item.L;

		// Color.FromArgb accepts "#RRGGBB"
		Color = Color.FromArgb(item.Hex);

		_proportion = item.Proportion;
	}


	/* ===SUMMARY
	 * Converts this segment back into a ColorItem when saving to the database.
	 */
	public ColorItem ToColorItem(int paletteId)
	{
		return new ColorItem
		{
			Id = ColorItemId,
			PaletteId = paletteId,
			Label = Label,
			Order = Order,
			Hex = Hex,
			H = H,
			S = S,
			L = L,
			Proportion = Proportion
		};
	}
}