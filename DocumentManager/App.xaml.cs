using System.Windows;
using DocumentManager.Services;
using DocumentManager.ViewModels;
using DocumentManager.Views;

namespace DocumentManager;

/// <summary>
/// Application entry point.
/// On first run, asks the user to set the common root folder.
/// Then prompts to create or open a project before showing the main window.
/// </summary>
public partial class App : Application
{
    private async void Application_Startup(object sender, StartupEventArgs e)
    {
        var settingsService = new SettingsService();

        // ── First-run: ask the user to pick the common root folder ──
        if (settingsService.IsFirstRun)
        {
            var settings = settingsService.Load();
            var setupDialog = new SetupDialog(settings.CommonRootPath);
            if (setupDialog.ShowDialog() != true)
            {
                Shutdown();
                return;
            }

            settings.CommonRootPath = setupDialog.SelectedRootPath;
            settingsService.Save(settings);
        }

        // ── Create and show the main window ──
        var mainWindow = new MainWindow();
        var vm = mainWindow.ViewModel;

        // Ensure the common root structure exists
        var appSettings = settingsService.Load();
        vm.SetCommonRoot(appSettings.CommonRootPath);
        mainWindow.Show();

        // ── Prompt to create or open a project ──
        // If there's a last-opened project, try to reopen it silently
        if (!string.IsNullOrEmpty(appSettings.LastProjectCode))
        {
            var projectPath = System.IO.Path.Combine(
                appSettings.CommonRootPath, "projects", appSettings.LastProjectCode, "metadata.json");
            if (System.IO.File.Exists(projectPath))
            {
                await vm.LoadProjectFromCodeAsync(appSettings.LastProjectCode);
                if (vm.IsProjectLoaded) return;
            }
        }

        // No previous project — ask the user
        var result = MessageBox.Show(
            "Welcome to LaTeX Document Manager!\n\n" +
            $"Main files folder: {appSettings.CommonRootPath}\n\n" +
            "Click 'Yes' to create a new project, or 'No' to open an existing one.",
            "LaTeX Document Manager",
            MessageBoxButton.YesNoCancel,
            MessageBoxImage.Information);

        switch (result)
        {
            case MessageBoxResult.Yes:
                vm.NewProjectCommand.Execute(null);
                // NewProjectCommand is async fire-and-forget, but the dialog
                // is modal — by the time it returns, the task has been queued.
                // We use a short dispatcher delay to let the async task complete.
                await WaitForProjectLoadedAsync(vm);
                if (!vm.IsProjectLoaded)
                {
                    MessageBox.Show("No project was created. The application will close.",
                        "LaTeX Document Manager", MessageBoxButton.OK, MessageBoxImage.Warning);
                    Shutdown();
                }
                break;

            case MessageBoxResult.No:
                vm.OpenProjectCommand.Execute(null);
                await WaitForProjectLoadedAsync(vm);
                if (!vm.IsProjectLoaded)
                {
                    MessageBox.Show("No project was opened. The application will close.",
                        "LaTeX Document Manager", MessageBoxButton.OK, MessageBoxImage.Warning);
                    Shutdown();
                }
                break;

            default:
                Shutdown();
                break;
        }
    }

    /// <summary>
    /// Waits a short time for the async command to complete and project to load.
    /// The dialog inside the command is modal so the actual work starts after it closes.
    /// </summary>
    private static async Task WaitForProjectLoadedAsync(MainViewModel vm)
    {
        // Give the async command time to finish (the dialog is synchronous,
        // but the file I/O is async after the dialog returns).
        for (var i = 0; i < 40; i++)
        {
            if (vm.IsProjectLoaded) return;
            await Task.Delay(50);
        }
    }
}
