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
        public IObservableCollection<MappingVM> Mappings => _Mappings;
        public SourceList<MappingVM> MappingsSource { get; } = new SourceList<MappingVM>();
        public ICommand AddCommand { get; }
        public ICommand HelpCommand { get; }
        public static string SettingsPath = Path.Combine(Environment.GetEnvironmentVariable("LocalAppData"), "BethesdaGitSync/Settings.xml");

        public MappingSettingsEditorVM MappingEditorVM { get; }

        public MainVM(MainWindow window)
        {
            this.Settings = Settings.Create_Xml(SettingsPath);
            this.MappingEditorVM = new MappingSettingsEditorVM(this);
            this.Settings.Mappings
                .Connect()
                .ObserveOn(RxApp.MainThreadScheduler)
                .Transform(m => new MappingVM(this, m))
                .Bind(this._Mappings)
                .DisposeMany()
                .Subscribe();
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
            window.Closed += (a, b) =>
            {
                this.Settings.Write_Xml(SettingsPath);
            };
        }
    }
}
