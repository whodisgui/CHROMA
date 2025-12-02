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
 *   - Shows list of saved palettes from the local SQLite database (ChromaDatabase)
 *   - Loads them and lets the user pick one and run critique
 *   - Run the CritiqueService over that palette (harmony spacing, contrast,
 *     similarity, and 60/30/10 balance)
 *   - Expose the current CritiqueReport to the UI (overall score + per-rule
 *     messages)
 *   - Maintain a simple history list of past critiques
 *   
 * This class is the "glue" between CritiquePage.xaml and the domain layer
 * (CritiqueService + ChromaDatabase). It contains no UI or platform-specific
 * code; everything is expressed as properties and commands for XAML bindings.
 */

public class CritiqueViewModel : BaseViewModel
{
	// ==== DEPENDENCIES ==========================================
	private readonly ChromaDatabase _database;			// persistence for palettes and critique reports
	private readonly CritiqueService _critiqueService;  // pure logic for the 4 critique tests

	public CritiqueViewModel()
	{
		_database = ChromaDatabase.Instance;
		_critiqueService = new CritiqueService();

		// Commands are exposed to XAML; they delegate to private async methods.
		LoadPalettesCommand = new Command(async () => await LoadPalettesAsync());
		LoadHistoryCommand = new Command(async () => await LoadHistoryAsync());
		RunCritiqueCommand = new Command(async () => await RunCritiqueAsync(),
			                             () => SelectedPalette != null && !IsBusy);

		// Initial data load when the page first appears
		_ = LoadPalettesAsync();
		_ = LoadHistoryAsync();
	}


	/* ==== INPUT HERE ==========================================
     * Palette selection section
     * The user chooses which saved palette to critique
    */

	public ObservableCollection<Palette> SavedPalettes { get; } = new();

	// Currently selected palette (may be null if nothing is chosen yet)
	private Palette? _selectedPalette;
	public Palette? SelectedPalette
	{
		get => _selectedPalette;
		set
		{
			// Notify bindings and re-evaluate whether RunCritique can execute
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

	// Simple busy flag so the UI can disable buttons/spinners while work is running
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
     * Holds the most recent CritiqueReport and displays a history list
     */

	// Most recent critique result for the currently selected palette
	private CritiqueReport? _currentReport;
	public CritiqueReport? CurrentReport
	{
		get => _currentReport;
		set
		{
			if (SetProperty(ref _currentReport, value, nameof(CurrentReport)))
			{
				// OverallScore and RuleResults are derived from CurrentReport
				OnPropertyChanged(nameof(OverallScore));
				OnPropertyChanged(nameof(RuleResults));
			}
		}
	}

	// Convenience property: numeric 0–100 overall score
	public int OverallScore => CurrentReport?.OverallScore ?? 0;

	// Convenience property: per-rule results for the current report
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
     * Private async helpers that implement the behavior of the commands.
     * These methods call into ChromaDatabase and CritiqueService
     * and then update the bound properties above.
    */

	// (Re)loads SavedPalettes from the database.
	async Task LoadPalettesAsync()
	{
		try
		{
			IsBusy = true;

			// Clear current critique feedback when refreshing palettes.
			CurrentReport = null;
			StatusMessage = string.Empty;

			SavedPalettes.Clear();

			var palettes = await _database.GetPalettesAsync();
			foreach (var p in palettes)
			{
				SavedPalettes.Add(p);
			}

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

	// Reloads critique history from the database.
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

	// Runs the four critique tests on the currently selected palette and saves the result.
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
