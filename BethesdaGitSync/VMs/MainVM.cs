using DynamicData;
using DynamicData.Binding;
using Noggog.WPF;
using ReactiveUI;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace BethesdaGitSync
{
    public class MainVM : ViewModel
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
        private Subject<Unit> _syncedToBinary = new Subject<Unit>();

        // Commands
        public ICommand AddCommand { get; }
        public ICommand HelpCommand { get; }
        public ReactiveCommand<Unit, Unit> SyncToGitCommand { get; }
        public ReactiveCommand<Unit, Unit> SyncToBinaryCommand { get; }
        public ReactiveCommand<Unit, Unit> SyncAllToGitCommand { get; }
        public ReactiveCommand<Unit, Unit> SyncAllToBinaryCommand { get; }

        private readonly ObservableAsPropertyHelper<bool> _SyncingGit;
        public bool SyncingGit => _SyncingGit.Value;

        private readonly ObservableAsPropertyHelper<bool> _SyncingBinary;
        public bool SyncingBinary => _SyncingBinary.Value;

        public MappingSettingsEditorVM MappingEditorVM { get; }

        public MainVM()
        {
        }

        public MainVM(MainWindow window)
        {
            // Create sub objects
            Instance = this;
            this.Settings = Settings.Create_Xml(SettingsPath);
            this.MappingEditorVM = new MappingSettingsEditorVM();

            // Some commands
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

            // Add default group if there is none
            if (this.Settings.Groupings.Count == 0)
            {
                this.Settings.Groupings.Add(new Grouping()
                {
                    Nickname = "Default"
                });
            }

            // Populate Group VMs
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

            // Save to disk when app closing
            window.Closed += (a, b) =>
            {
                this.Settings.Write_Xml(SettingsPath);
            };

            // Sync commands
            IObservable<bool> anySelected = this.WhenAny(x => x.SelectedGroup.SelectedMappings.CountChanged)
                .Switch()
                .Select(c => c > 0)
                .Publish()
                .RefCount();
            this.SyncToGitCommand = ReactiveCommand.CreateFromTask(
                execute: () => SyncToGit(this.SelectedGroup?.SelectedMappings.ToArray()),
                canExecute: anySelected);
            window.Events().KeyDown
                .Keybind(Key.G, ModifierKeys.Control)
                .InvokeCommand(this.SyncToGitCommand)
                .DisposeWith(this.CompositeDisposable);
            this.SyncToBinaryCommand = ReactiveCommand.CreateFromTask(
                execute: () => SyncToBinary(this.SelectedGroup?.SelectedMappings.ToArray()),
                canExecute: anySelected);
            window.Events().KeyDown
                .Keybind(Key.B, ModifierKeys.Control)
                .InvokeCommand(this.SyncToBinaryCommand)
                .DisposeWith(this.CompositeDisposable);
            this.SyncAllToGitCommand = ReactiveCommand.CreateFromTask(
                execute: () => SyncToGit(this.SelectedGroup?.Mappings.ToArray()));
            window.Events().KeyDown
                .Keybind(Key.G, ModifierKeys.Control | ModifierKeys.Shift)
                .InvokeCommand(this.SyncAllToGitCommand)
                .DisposeWith(this.CompositeDisposable);
            this.SyncAllToBinaryCommand = ReactiveCommand.CreateFromTask(
                execute: () => SyncToBinary(this.SelectedGroup?.Mappings.ToArray()));
            window.Events().KeyDown
                .Keybind(Key.B, ModifierKeys.Control | ModifierKeys.Shift)
                .InvokeCommand(this.SyncAllToBinaryCommand)
                .DisposeWith(this.CompositeDisposable);
            _SyncingGit = Observable.CombineLatest(
                    this.SyncToGitCommand.IsExecuting,
                    this.SyncAllToGitCommand.IsExecuting,
                    resultSelector: (s, a) => s || a)
                .ToProperty(this, nameof(SyncingGit));
            _SyncingBinary = Observable.CombineLatest(
                    this.SyncToBinaryCommand.IsExecuting,
                    this.SyncAllToBinaryCommand.IsExecuting,
                    resultSelector: (s, a) => s || a)
                .ToProperty(this, nameof(SyncingBinary));

            // Add delete keybind
            window.Events().KeyDown
                .Keybind(Key.Delete)
                .Subscribe(q =>
                {
                    var group = this.SelectedGroup;
                    this.SelectedGroup.Settings.Mappings.RemoveMany(
                        this.SelectedGroup.SelectedMappings
                            .Select(g => g.Mapping)
                            .ToArray());
                        
                });
        }

        private async Task SyncToGit(IEnumerable<MappingVM> toSync)
        {
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
        }

        private async Task SyncToBinary(IEnumerable<MappingVM> toSync)
        {
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
        }
    }
}
