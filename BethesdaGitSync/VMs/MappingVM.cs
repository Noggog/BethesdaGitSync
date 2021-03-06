﻿using Mutagen.Bethesda;
using Mutagen.Bethesda.GitSync;
using Noggog;
using Noggog.Utility;
using Noggog.WPF;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using Splat;

namespace BethesdaGitSync
{
    public enum StatusType
    {
        None,
        Warning,
        Error,
        Success
    }

    public class MappingVM : ReactiveObject, IEnableLogger
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
        public ICommand NavigateToBinaryCommand { get; }
        public ICommand NavigateToFolderCommand { get; }
        public ICommand NavigateToBackupCommand { get; }

        private bool _IsSelected;
        public bool IsSelected { get => _IsSelected; set => this.RaiseAndSetIfChanged(ref _IsSelected, value); }

        private Subject<Unit> flashSubj = new Subject<Unit>();
        private readonly ObservableAsPropertyHelper<bool> _Flash;
        public bool Flash => _Flash.Value;

        private readonly ObservableAsPropertyHelper<string> _BackupPath;
        public string BackupPath => _BackupPath.Value;

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
            this.NavigateToBinaryCommand = ReactiveCommand.Create(
                execute: () => ShowSelectedInExplorer.FileOrFolder(this.Mapping.BinaryPath.Path));
            this.NavigateToFolderCommand = ReactiveCommand.Create(
                execute: () => ShowSelectedInExplorer.FileOrFolder(this.Mapping.FolderPath.Path));
            this.NavigateToBackupCommand = ReactiveCommand.Create(
                execute: () => ShowSelectedInExplorer.FileOrFolder(this.BackupPath));

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

            _Flash = WPFObservableUtility.FlipFlop(flashSubj, TimeSpan.FromMilliseconds(400))
                .ToProperty(this, nameof(Flash));

            this._BackupPath = this.WhenAny(x => x.Nickname)
                .Select(nickname => nickname == null ? MainVM.BackupPath : Path.Combine(MainVM.BackupPath, nickname))
                .ToProperty(this, nameof(BackupPath));
        }

        private static string ConstructErrorMessage(GitConversionUtility.Error err, FilePath binaryPath, string sourcePath)
        {
            switch (err)
            {
                case GitConversionUtility.Error.None:
                    return null;
                case GitConversionUtility.Error.ModKey:
                    return $"Could not construct a ModKey from given binary path: {binaryPath.Name}.  Expected .esp/.esm file type.";
                case GitConversionUtility.Error.DidNotExist:
                    return $"Source path did not exist: {sourcePath}.";
                case GitConversionUtility.Error.Corrupted:
                    return $"Correctness logic detected corruption in the sync.  Cancelled.";
                default:
                    throw new NotImplementedException();
            }
        }

        public async Task SyncToGit()
        {
            try
            {
                this.Log().Write($"Syncing {this.Nickname} to git.", LogLevel.Info);
                var err = await GitConversionUtility.ConvertToFolder(
                    this.Mapping.BinaryPath,
                    this.Mapping.FolderPath,
                    GitConversionInstructions.Oblivion(ModKey.Dummy),
                    checkCorrectness: true,
                    backupFolder: this.BackupPath);
                this.LastFolderError = ConstructErrorMessage(err, this.Mapping.BinaryPath, this.Mapping.FolderPath.Path);
                this.Log().Write($"Synced {this.Nickname} to git: {this.LastFolderError}", LogLevel.Info);
            }
            catch (Exception ex)
            {
                this.LastFolderError = ex.Message;
            }
            flashSubj.OnNext(Unit.Default);
        }

        public async Task SyncToBinary()
        {
            try
            {
                this.Log().Write($"Syncing {this.Nickname} to binary.", LogLevel.Info);
                var err = await GitConversionUtility.ConvertToBinary(
                    this.Mapping.FolderPath,
                    this.Mapping.BinaryPath,
                    GitConversionInstructions.Oblivion(ModKey.Dummy),
                    checkCorrectness: true,
                    backupFolder: this.BackupPath);
                this.LastBinaryError = ConstructErrorMessage(err, this.Mapping.BinaryPath, this.Mapping.FolderPath.Path);
                this.Log().Write($"Synced {this.Nickname} to binary: {this.LastBinaryError}", LogLevel.Info);
            }
            catch (Exception ex)
            {
                this.LastFolderError = ex.Message;
            }
            flashSubj.OnNext(Unit.Default);
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
