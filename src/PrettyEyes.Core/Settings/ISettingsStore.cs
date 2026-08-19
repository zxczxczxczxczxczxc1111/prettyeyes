namespace PrettyEyes.Core.Settings;

public interface ISettingsStore
{
    AppSettings Load();

    void Save(AppSettings settings);
}
