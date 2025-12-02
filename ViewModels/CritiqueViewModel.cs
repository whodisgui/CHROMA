using System.Windows.Input;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using CHROMA.Data;
using CHROMA.Models;
using CHROMA.Services;

namespace CHROMA.ViewModels;

/* ===FILE SUMMARY===
 * Main view‑model for the "Critique" tab/page in CHROMA.
 * 
 * Responsibilities:
 *   - Shows list of saved palettes from the database
 *   - Lets the user pick one and run critique
 *   - Shows current critique (overall score + per-rule results)
 *   - Shows simple history of past critiques
 */

public class CritiqueViewModel : BaseViewModel
{
	private readonly ChromaDatabase _database;
	private readonly CritiqueService _critiqueService;

	public CritiqueViewModel()
	{
		_database = ChromaDatabase.Instance;
		_critiqueService = new CritiqueService();

		LoadPalettesCommand = new Command(async () => await LoadPalettesAsync());
		LoadHistoryCommand = new Command(async () => await LoadHistoryAsync());
		RunCritiqueCommand = new Command(
			async () => await RunCritiqueAsync(),
			() => SelectedPalette != null && !IsBusy);

		// Initial load
		_ = LoadPalettesAsync();
		_ = LoadHistoryAsync();
	}


	/* ==== INPUT HERE ==========================================
     * Select a palette currently saved on the database.
    */

	public ObservableCollection<Palette> SavedPalettes { get; } = new();

	private Palette? _selectedPalette;
	public Palette? SelectedPalette
	{
		get => _selectedPalette;
		set
		{
			if (SetProperty(ref _selectedPalette, value, nameof(SelectedPalette)))
			{
				((Command)RunCritiqueCommand).ChangeCanExecute();
			}
		}
	}

	// Status line for small informational messages
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
		set
		{
			if (SetProperty(ref _isBusy, value, nameof(IsBusy)))
			{
				((Command)RunCritiqueCommand).ChangeCanExecute();
			}
		}
	}


	/* ==== CRITIQUE/FEEDBACK SECTION ===========================
     * Runs CritiqueService based on inputted palette
     */

	private CritiqueReport? _currentReport;
	public CritiqueReport? CurrentReport
	{
		get => _currentReport;
		set
		{
			if (SetProperty(ref _currentReport, value, nameof(CurrentReport)))
			{
				OnPropertyChanged(nameof(OverallScore));
				OnPropertyChanged(nameof(RuleResults));
			}
		}
	}

	public int OverallScore => CurrentReport?.OverallScore ?? 0;

	public IReadOnlyList<CritiqueRuleResult> RuleResults =>
	    CurrentReport?.RuleResults ?? new List<CritiqueRuleResult>();

	public ObservableCollection<CritiqueReport> History { get; } = new();


	/* ==== COMMANDS ============================================
     * Commands are bound to buttons in XAML to trigger actions from the UI layer.
    */

	public ICommand LoadPalettesCommand { get; }
	public ICommand LoadHistoryCommand { get; }
	public ICommand RunCritiqueCommand { get; }

	/* ==== CORE LOGIC ==========================================
     * Implementation of the commands' behaviors.
    */

	async Task LoadPalettesAsync()
	{
		try
		{
			IsBusy = true;
			SavedPalettes.Clear();

			var palettes = await _database.GetPalettesAsync();
			foreach (var p in palettes)
				SavedPalettes.Add(p);

			StatusMessage = SavedPalettes.Count == 0
				? "No saved palettes yet. Create one on the Create tab first."
				: $"Loaded {SavedPalettes.Count} saved palette(s).";
		}
		catch (Exception ex)
		{
			StatusMessage = "Error loading palettes. See debug output.";
			Debug.WriteLine(ex);
		}
		finally
		{
			IsBusy = false;
		}
	}

	async Task LoadHistoryAsync()
	{
		try
		{
			IsBusy = true;
			History.Clear();

			var reports = await _database.GetCritiqueReportsAsync();
			foreach (var r in reports)
				History.Add(r);
		}
		catch (Exception ex)
		{
			StatusMessage = "Error loading critique history. See debug output.";
			Debug.WriteLine(ex);
		}
		finally
		{
			IsBusy = false;
		}
	}

	async Task RunCritiqueAsync()
	{
		if (SelectedPalette == null)
		{
			StatusMessage = "Select a saved palette before running critique.";
			return;
		}

		try
		{
			IsBusy = true;
			StatusMessage = "Running critique...";

			// Load this palette's colors from the DB.
			var colors = await _database.GetColorsForPaletteAsync(SelectedPalette.Id);
			if (colors.Count == 0)
			{
				StatusMessage = "Selected palette has no colors stored.";
				return;
			}

			// Run the engine
			var report = _critiqueService.EvaluatePalette(SelectedPalette, colors);

			// Update current view
			CurrentReport = report;

			// Persist the report for history
			int reportId = await _database.SaveCritiqueReportAsync(report);
			report.Id = reportId;

			// Insert at top of history list
			History.Insert(0, report);

			StatusMessage = "Critique completed.";
		}
		catch (Exception ex)
		{
			StatusMessage = "Error running critique. See debug output.";
			Debug.WriteLine(ex);
		}
		finally
		{
			IsBusy = false;
		}
	}
}
