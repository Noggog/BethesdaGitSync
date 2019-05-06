using DynamicData;
using DynamicData.Binding;
using Noggog.WPF;
using ReactiveUI;
using System;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace BethesdaGitSync
{
    public class MainVM : ReactiveObject
    {
        // Static constants
        public static MainVM Instance { get; private set; }
        public const string AppName = "BethesdaGitSync";
        public static readonly string SettingsPath = Path.Combine(Environment.GetEnvironmentVariable("LocalAppData"), $"{AppName}/Settings.xml");
        public static readonly string BackupPath = Path.Combine(System.IO.Path.GetTempPath(), AppName);

        public Settings Settings { get; }
        public ObservableCollectionExtended<GroupingVM> Groupings { get; } = new ObservableCollectionExtended<GroupingVM>();
        private GroupingVM _SelectedGroup;
        public GroupingVM SelectedGroup { get => _SelectedGroup; set => this.RaiseAndSetIfChanged(ref _SelectedGroup, value); }

        // Signals
        private Subject<Unit> _syncedToGit = new Subject<Unit>();
        private readonly ObservableAsPropertyHelper<bool> _SyncedToGitFlash;
        public bool SyncedToGitFlash => _SyncedToGitFlash.Value;
        private Subject<Unit> _syncedToBinary = new Subject<Unit>();
        private readonly ObservableAsPropertyHelper<bool> _SyncedToBinaryFlash;
        public bool SyncedToBinaryFlash => _SyncedToBinaryFlash.Value;

        // Commands
        public ICommand AddCommand { get; }
        public ICommand HelpCommand { get; }
        public ICommand SyncToGitCommand { get; }
        public ICommand SyncToBinaryCommand { get; }

        public MappingSettingsEditorVM MappingEditorVM { get; }

        public MainVM(MainWindow window)
        {
            Instance = this;
            this.Settings = Settings.Create_Xml(SettingsPath);
            this.MappingEditorVM = new MappingSettingsEditorVM();
            if (this.Settings.Groupings.Count == 0)
            {
                this.Settings.Groupings.Add(new Grouping()
                {
                    Nickname = "Default"
                });
            }
            this.Settings.Groupings.Connect()
                .ObserveOn(RxApp.MainThreadScheduler)
                .Transform(g => new GroupingVM(g))
                .DisposeMany()
                .Bind(this.Groupings)
                // Set selected group to first item on list
                .QueryWhenChanged(query => query.FirstOrDefault())
                .CombineLatest(
                    this.WhenAny(x => x.SelectedGroup)
                        .DistinctUntilChanged(),
                    resultSelector: (First, Selected) => (First, Selected))
                .Subscribe(tup =>
                {
                    if (tup.Selected == null)
                    {
                        this.SelectedGroup = tup.First;
                    }
                });
            this.AddCommand = ReactiveCommand.Create(
                execute: () =>
                {
                    this.MappingEditorVM.Target(
                        new MappingVM(
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
                    var toSync = this.SelectedGroup?.SelectedMappings.ToArray();
                    if (toSync == null) return;
                    await Task.Run(async () =>
                    {
                        await Task.WhenAll(toSync.Select(item =>
                        {
                            return Task.Run(async () =>
                            {
                                await item.SyncToGit();
                                GC.Collect();
                            });
                        }));
                        _syncedToGit.OnNext(Unit.Default);
                    });
                },
                canExecute: this.WhenAny(x => x.SelectedGroup.SelectedMappings.CountChanged)
                    .Switch()
                    .Select(c => c > 0));

            this.SyncToBinaryCommand = ReactiveCommand.CreateFromTask(
                execute: async () =>
                {
                    var toSync = this.SelectedGroup?.SelectedMappings.ToArray();
                    if (toSync == null) return;
                    await Task.Run(async () =>
                    {
                        await Task.WhenAll(toSync.Select(item =>
                        {
                            return Task.Run(async () =>
                            {
                                try
                                {
                                    await item.SyncToBinary();
                                    GC.Collect();
                                }
                                catch (Exception ex)
                                {
                                    item.LastBinaryError = ex.Message;
                                }
                            });
                        }));
                        _syncedToBinary.OnNext(Unit.Default);
                    });
                },
                canExecute: this.WhenAny(x => x.SelectedGroup.SelectedMappings.CountChanged)
                    .Switch()
                    .Select(c => c > 0));

            _SyncedToBinaryFlash = ObservableUtility.FlipFlop(_syncedToBinary, TimeSpan.FromMilliseconds(400))
                .ToProperty(this, nameof(SyncedToBinaryFlash));
            _SyncedToGitFlash = ObservableUtility.FlipFlop(_syncedToGit, TimeSpan.FromMilliseconds(400))
                .ToProperty(this, nameof(SyncedToGitFlash));
        }
    }
}
