using CHROMA.Models;
using Microsoft.Maui.Storage;
using SQLite;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

/* ===SUMMARY===
 * Low-level SQLite access for Models (Palette, ColorItem, & CritiqueReport)
 * Follows the pattern from public MAUI Documentations:
 * https://learn.microsoft.com/en-us/dotnet/maui/data-cloud/database-sqlite?view=net-maui-10.0
 * lazy-initialize a SQLiteAsyncConnection and expose async CRUD helpers.
 */

namespace CHROMA.Data
{
	public class ChromaDatabase
	{
		// === Singleton Boilerplate ===============
		private static readonly Lazy<ChromaDatabase> _lazyInstance =
			new(() => new ChromaDatabase());

		// Global singletonn instance of database.
		public static ChromaDatabase Instance => _lazyInstance.Value;

		// Constructor is private so it can only be created via Instance
		private ChromaDatabase()
		{
			// your existing init logic stays here:
			// - maybe nothing
			// Do not call InitAsync() here; it is async
			// The first actual DB call will trigger InitAsync()
		}

		// ========== SQLite Connection Setup ==============================
		const string DatabaseFilename = "chroma.db3";

		// Recommended flags from docs: ReadWrite, Create, SharedCache.
		const SQLiteOpenFlags Flags =
			SQLiteOpenFlags.ReadWrite |
			SQLiteOpenFlags.Create |
			SQLiteOpenFlags.SharedCache;

		SQLiteAsyncConnection? _connection;


		async Task InitAsync()
		{
			if (_connection is not null)
				return;

			var path = Path.Combine(FileSystem.AppDataDirectory, DatabaseFilename);
			_connection = new SQLiteAsyncConnection(path, Flags);

			// Create tables if they don't exist yet.
			await _connection.CreateTableAsync<Palette>();
			await _connection.CreateTableAsync<ColorItem>();
			await _connection.CreateTableAsync<CritiqueReport>();
		}

		// ========== CRUD Helpers ==============================

		// Saves a new palette and its colors. Currently only supports Create (no update).
		public async Task<int> SavePaletteAsync(Palette palette, IEnumerable<ColorItem> colors)
		{
			await InitAsync();
			if (_connection is null)
				throw new InvalidOperationException("Database not initialized.");

			// Insert palette (Id gets set by SQLite).
			await _connection.InsertAsync(palette);

			var colorList = colors.ToList();
			foreach (var color in colorList)
			{
				color.PaletteId = palette.Id;
			}

			if (colorList.Count > 0)
				await _connection.InsertAllAsync(colorList);

			return palette.Id;
		}

		// Returns all palettes
		public async Task<List<Palette>> GetPalettesAsync()
		{
			await InitAsync();
			if (_connection is null)
				throw new InvalidOperationException("Database not initialized.");

			return await _connection.Table<Palette>()
				                    .OrderByDescending(p => p.CreatedUtc)
									.ToListAsync();
		}

		// Returns all colors belonging to a given palette, in slot order.
		public async Task<List<ColorItem>> GetColorsForPaletteAsync(int paletteId)
		{
			await InitAsync();
			if (_connection is null)
				throw new InvalidOperationException("Database not initialized.");

			return await _connection.Table<ColorItem>()
									.Where(c => c.PaletteId == paletteId)
									.OrderBy(c => c.Order)
									.ToListAsync();
		}

		// Deletes a palette and, optionally, its associated colors.
		public async Task<int> DeletePaletteAsync(Palette palette, bool deleteColors = true)
		{
			await InitAsync();
			if (_connection is null)
				throw new InvalidOperationException("Database not initialized.");

			if (deleteColors)
			{
				var colors = await GetColorsForPaletteAsync(palette.Id);
				foreach (var c in colors)
					await _connection.DeleteAsync(c);
			}

			return await _connection.DeleteAsync(palette);
		}
	}
}