using Avalonia.Controls;
using Avalonia.Platform.Storage;
using PrettyEyes.Core.Diagnostics;
using PrettyEyes.Core.Rendering;
using PrettyEyes.Core.Settings;
using PrettyEyes.Platform.Windows;

namespace PrettyEyes.App.Views;

/// <summary>
/// Where a copied screenshot lands by itself, and under what name. Behind the
/// quick save icon now.
/// </summary>
public partial class QuickSaveSettingsView : UserControl
{
    private bool _loading;

    public QuickSaveSettingsView()
    {
        InitializeComponent();

        Autosave.IsCheckedChanged += (_, _) =>
        {
            var enabled = Autosave.IsChecked == true;

            ShowState(enabled);

            if (!_loading)
            {
                Changed?.Invoke(this, save => save with { Enabled = enabled });
            }
        };

        SaveTemplate.TextChanged += (_, _) =>
        {
            ShowExample();

            if (!_loading)
            {
                Changed?.Invoke(this, save => save with { Template = SaveTemplate.Text ?? string.Empty });
            }
        };

        PickFolder.Click += async (_, _) => await PickAsync();
    }

    /// <summary>One field moved. The window owns the settings record.</summary>
    public event EventHandler<Func<SaveOptions, SaveOptions>>? Changed;

    /// <summary>Something went wrong and the person has to hear about it.</summary>
    public event EventHandler<string>? Failed;

    /// <summary>The checkbox moved, so the icon in the settings has to follow.</summary>
    public event EventHandler? Toggled;

    public void Load(SaveOptions save)
    {
        _loading = true;

        Autosave.IsChecked = save.Enabled;
        SaveFolder.Text = save.Folder;
        SaveTemplate.Text = save.Template;
        ShowState(save.Enabled);
        ShowExample();

        _loading = false;
    }

    /// <summary>Sets the checkbox without reporting it: the caller just did.</summary>
    public void ShowEnabled(bool enabled)
    {
        _loading = true;
        Autosave.IsChecked = enabled;
        ShowState(enabled);
        _loading = false;
    }

    /// <summary>
    /// Switched off, the group stays where it is, greyed out. Hiding it would
    /// mean looking for a setting that is not on the screen.
    /// </summary>
    private void ShowState(bool enabled)
    {
        AutosaveOptions.IsEnabled = enabled;
        AutosaveOptions.Opacity = enabled ? 1 : 0.4;

        if (!_loading)
        {
            Toggled?.Invoke(this, EventArgs.Empty);
        }
    }

    private void ShowExample() =>
        SaveExample.Text = "например: "
            + FileNameTemplate.Format(SaveTemplate.Text ?? string.Empty, DateTimeOffset.Now);

    /// <summary>
    /// The folder is checked the moment it is picked, not when a screenshot is
    /// waiting on it: a disconnected network drive answers slowly, and that is
    /// not a delay to discover mid-capture.
    /// </summary>
    private async Task PickAsync()
    {
        var storage = TopLevel.GetTopLevel(this)?.StorageProvider;

        if (storage is null)
        {
            return;
        }

        try
        {
            var picked = await storage.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = "Куда сохранять скриншоты",
                AllowMultiple = false,
            });

            var folder = picked.FirstOrDefault()?.TryGetLocalPath();

            if (string.IsNullOrWhiteSpace(folder))
            {
                return;
            }

            if (!FolderSink.CanWrite(folder))
            {
                Failed?.Invoke(this, "В эту папку нельзя писать. Выбери другую.");

                return;
            }

            SaveFolder.Text = folder;
            Changed?.Invoke(this, save => save with { Folder = folder });
        }
        catch (Exception error)
        {
            Log.Default.Error("не удалось выбрать папку", error);
            Failed?.Invoke(this, "Не удалось открыть выбор папки.");
        }
    }
}
