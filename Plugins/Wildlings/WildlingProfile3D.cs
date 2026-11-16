using System.Text.Json;
using ByteEngine.Core.Scene;

namespace Wildlings;

/// <summary>Serialisable player profile (GDD v0.3 slice 3).</summary>
public sealed class WildlingProfileState
{
    public int Coins { get; set; }

    public int Stones { get; set; }

    public int Charms { get; set; }

    public int FireLizardEssence { get; set; }

    public int SprigletEssence { get; set; }

    public int ZappetEssence { get; set; }

    public List<string> OwnedSpecies { get; set; } = new() { WildlingSpeciesDatabase.FireLizardId };

    public string EquippedSpecies { get; set; } = WildlingSpeciesDatabase.FireLizardId;

    public int Runs { get; set; }

    public int Clears { get; set; }

    public int BestKills { get; set; }

    public float BestSurvivalSeconds { get; set; }
}

/// <summary>
/// Persistent Wildlings profile: coins, essence, owned creatures and run history.
///
/// Saved as JSON next to the project (folder is a property), so the Sanctuary can read it
/// without any hard-coded path or embedded numbers. Rewards are idempotent per run: the
/// run director calls <see cref="RecordRun"/> once through its own guard.
/// </summary>
public sealed class WildlingProfile3D : Component
{
    /// <summary>Folder under the project root; created on first save.</summary>
    public string SaveFolder { get; set; } = "Saves";

    public string FileName { get; set; } = "wildlings-profile.json";

    public bool AutoLoad { get; set; } = true;

    public bool AutoSave { get; set; } = true;

    public int StartingCoins { get; set; }

    public WildlingProfileState State = new();

    public string SavePath
    {
        get
        {
            string? root = GameObject?.Scene?.ProjectRoot;
            return string.IsNullOrWhiteSpace(root) ? FileName : Path.Combine(root, SaveFolder, FileName);
        }
    }

    protected override void OnStart()
    {
        if (AutoLoad)
        {
            Load();
        }
    }

    /// <summary>Reads the profile from disk; a missing or unreadable file keeps defaults.</summary>
    public void Load()
    {
        try
        {
            string path = SavePath;

            if (!File.Exists(path))
            {
                State = new WildlingProfileState { Coins = StartingCoins };
                return;
            }

            State = JsonSerializer.Deserialize<WildlingProfileState>(File.ReadAllText(path))
                    ?? new WildlingProfileState { Coins = StartingCoins };
        }
        catch
        {
            State = new WildlingProfileState { Coins = StartingCoins };
        }
    }

    /// <summary>Writes the profile to disk.</summary>
    public void Save()
    {
        try
        {
            string path = SavePath;
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, JsonSerializer.Serialize(State, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch
        {
            // A read-only or sandboxed build must not crash the run; rewards stay in memory.
        }
    }

    public void AddCoins(int amount) => State.Coins = Math.Max(0, State.Coins + amount);

    /// <summary>Adds essence for a species id (<c>fire_lizard</c>, <c>spriglet</c>, <c>zappet</c>).</summary>
    public void AddEssence(string speciesId, int amount)
    {
        if (amount == 0)
        {
            return;
        }

        if (string.Equals(speciesId, WildlingSpeciesDatabase.SprigletId, StringComparison.OrdinalIgnoreCase))
        {
            State.SprigletEssence = Math.Max(0, State.SprigletEssence + amount);
        }
        else if (string.Equals(speciesId, WildlingSpeciesDatabase.ZappetId, StringComparison.OrdinalIgnoreCase))
        {
            State.ZappetEssence = Math.Max(0, State.ZappetEssence + amount);
        }
        else
        {
            State.FireLizardEssence = Math.Max(0, State.FireLizardEssence + amount);
        }
    }

    /// <summary>Owns a species (no duplicates).</summary>
    public void Own(string speciesId)
    {
        if (!string.IsNullOrWhiteSpace(speciesId) &&
            !State.OwnedSpecies.Contains(speciesId, StringComparer.OrdinalIgnoreCase))
        {
            State.OwnedSpecies.Add(speciesId);
        }
    }

    /// <summary>Records one finished run and banks its rewards.</summary>
    public void RecordRun(bool cleared, int kills, float seconds, int coins, string speciesId, int essence)
    {
        State.Runs++;
        State.BestKills = Math.Max(State.BestKills, kills);
        State.BestSurvivalSeconds = MathF.Max(State.BestSurvivalSeconds, seconds);

        if (cleared)
        {
            State.Clears++;
        }

        AddCoins(coins);
        AddEssence(speciesId, essence);

        if (AutoSave)
        {
            Save();
        }
    }
}
