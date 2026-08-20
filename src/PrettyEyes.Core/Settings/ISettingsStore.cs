namespace PrettyEyes.Core.Settings;

public interface ISettingsStore
{
    AppSettings Load();

    /// <summary>False when the settings could not be written down.</summary>
    bool Save(AppSettings settings);
}
