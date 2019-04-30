using DynamicData;
using DynamicData.Binding;
using ReactiveUI;
using System;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace BethesdaGitSync
{
    public class MainVM : ReactiveObject
    {
        public Settings Settings { get; }
        private ObservableCollectionExtended<MappingVM> _Mappings { get; } = new ObservableCollectionExtended<MappingVM>();
        private IObservableList<MappingVM> _SelectedMappings;
        public IObservableCollection<MappingVM> Mappings => _Mappings;
        public ICommand AddCommand { get; }
        public ICommand HelpCommand { get; }
        public ICommand SyncToGitCommand { get; }
        public ICommand SyncToBinaryCommand { get; }
        public const string AppName = "BethesdaGitSync";
        public static readonly string SettingsPath = Path.Combine(Environment.GetEnvironmentVariable("LocalAppData"), $"{AppName}/Settings.xml");
        public static readonly string BackupPath = Path.Combine(System.IO.Path.GetTempPath(), AppName);

        public MappingSettingsEditorVM MappingEditorVM { get; }

        public MainVM(MainWindow window)
        {
            this.Settings = Settings.Create_Xml(SettingsPath);
            this.MappingEditorVM = new MappingSettingsEditorVM(this);
            this._SelectedMappings = this.Settings.Mappings
                .Connect()
                .ObserveOn(RxApp.MainThreadScheduler)
                .Transform(m => new MappingVM(this, m))
                .DisposeMany()
                // Bind to visible mappings for GUI
                .Bind(this._Mappings)
                // Filter ones that are selected, and save to collection
                .AutoRefresh(m => m.IsSelected)
                .Filter(m => m.IsSelected)
                .AsObservableList();
            this.AddCommand = ReactiveCommand.Create(
                execute: () =>
                {
                    this.MappingEditorVM.Target(
                        new MappingVM(
                            this,
                            new Mapping()),
                        newItem: true);
                });
            this.HelpCommand = ReactiveCommand.Create(
                execute: () => { });

            // Save to disk when app closing
            window.Closed += (a, b) =>
            {
                this.Settings.Write_Xml(SettingsPath);
            };

            this.SyncToGitCommand = ReactiveCommand.CreateFromTask(
                execute: async () =>
                {
                    var toSync = this._SelectedMappings.ToArray();
                    foreach (var item in toSync)
                    {
                        try
                        {
                            await item.SyncToGit();
                        }
                        catch (Exception ex)
                        {
                            item.LastFolderError = ex.Message;
                        }
                    }
                },
                canExecute: this._SelectedMappings
                    .CountChanged
                    .Select(c => c > 0));

            this.SyncToBinaryCommand = ReactiveCommand.CreateFromTask(
                execute: async () =>
                {
                    var toSync = this._SelectedMappings.ToArray();
                    foreach (var item in toSync)
                    {
                        try
                        {
                            await item.SyncToBinary();
                        }
                        catch (Exception ex)
                        {
                            item.LastBinaryError = ex.Message;
                        }
                    }
                },
                canExecute: this._SelectedMappings
                    .CountChanged
                    .Select(c => c > 0));
        }
    }
}
