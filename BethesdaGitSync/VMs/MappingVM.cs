using Mutagen.Bethesda;
using Mutagen.Bethesda.GitSync;
using Noggog;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace BethesdaGitSync
{
    public enum StatusType
    {
        None,
        Warning,
        Error,
        Success
    }

    public class MappingVM : ReactiveObject
    {
        public Mapping Mapping { get; }

        private readonly ObservableAsPropertyHelper<string> _Nickname;
        public string Nickname => _Nickname.Value;

        public class StatusPair
        {
            public StatusType Status { get; set; }
            public string Message { get; set; }
        }

        private readonly ObservableAsPropertyHelper<StatusPair> _BinaryStatus;
        public StatusPair BinaryStatus => _BinaryStatus.Value;

        private readonly ObservableAsPropertyHelper<StatusPair> _FolderStatus;
        public StatusPair FolderStatus => _FolderStatus.Value;

        private string _LastBinaryError;
        public string LastBinaryError { get => _LastBinaryError; set => this.RaiseAndSetIfChanged(ref _LastBinaryError, value); }

        private string _LastFolderError;
        public string LastFolderError { get => _LastFolderError; set => this.RaiseAndSetIfChanged(ref _LastFolderError, value); }

        public ICommand OpenSettingsCommand { get; }

        private bool _IsSelected;
        public bool IsSelected { get => _IsSelected; set => this.RaiseAndSetIfChanged(ref _IsSelected, value); }

        public MappingVM(Mapping mapping)
        {
            this.Mapping = mapping;

            this.OpenSettingsCommand = ReactiveCommand.Create(
                execute: () =>
                {
                    MainVM.Instance.MappingEditorVM.Target(
                        this,
                        newItem: false);
                });

            this._Nickname = Observable.CombineLatest(
                    this.WhenAny(x => x.Mapping.Nickname),
                    this.WhenAny(x => x.Mapping.BinaryPath)
                        .Select(binary => binary.Name),
                    (nickname, binary) => string.IsNullOrWhiteSpace(nickname) ? binary : nickname)
                .ToProperty(this, nameof(Nickname));

            this._BinaryStatus = GetStatusObservable(
                    this.WhenAny(x => x.Mapping.BinaryPath)
                        .Cast<FilePath, IPath>(),
                    this.WhenAny(x => x.LastBinaryError))
                .ToProperty(this, nameof(BinaryStatus));

            this._FolderStatus = GetStatusObservable(
                this.WhenAny(x => x.Mapping.FolderPath)
                    .Cast<DirectoryPath, IPath>(),
                this.WhenAny(x => x.LastFolderError))
                .ToProperty(this, nameof(FolderStatus));
        }

        public async Task SyncToGit()
        {
            await GitConversionUtility.ConvertToFolder(
                this.Mapping.BinaryPath,
                this.Mapping.FolderPath,
                GitConversionInstructions.Oblivion(ModKey.Dummy),
                checkCorrectness: true,
                backupFolder: Path.Combine(MainVM.BackupPath, this.Nickname));
        }

        public async Task SyncToBinary()
        {
            await GitConversionUtility.ConvertToBinary(
                this.Mapping.FolderPath,
                this.Mapping.BinaryPath,
                GitConversionInstructions.Oblivion(ModKey.Dummy),
                checkCorrectness: true,
                backupFolder: Path.Combine(MainVM.BackupPath, this.Nickname));
        }

        private static IObservable<StatusPair> GetStatusObservable(
            IObservable<IPath> pathObs,
            IObservable<string> lastErrorObs)
        {
            var pathExistsObs = Observable.Merge(
                pathObs,
                Observable.Interval(TimeSpan.FromSeconds(1))
                    .WithLatestFrom(
                        pathObs,
                        resultSelector: (timer, bin) => bin))
                .Select(bin => (bin.Path, bin.Exists))
                .DistinctUntilChanged();

            return Observable.CombineLatest(
                pathExistsObs,
                lastErrorObs,
                resultSelector: (exists, lastErr) =>
                {
                    if (!string.IsNullOrEmpty(lastErr))
                    {
                        return new StatusPair()
                        {
                            Message = lastErr,
                            Status = StatusType.Error
                        };
                    }
                    if (!exists.Exists)
                    {
                        return new StatusPair()
                        {
                            Message = $"Path did not exist: {exists.Path}",
                            Status = StatusType.Warning
                        };
                    }
                    return new StatusPair()
                    {
                        Status = StatusType.None
                    };
                });
        }
    }
}
