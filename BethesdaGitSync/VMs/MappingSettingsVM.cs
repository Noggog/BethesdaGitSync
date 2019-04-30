using BethesdaGitSync.Internals;
using Loqui;
using Noggog;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Input;

namespace BethesdaGitSync
{
    public class MappingSettingsEditorVM : ReactiveObject
    {
        private MappingVM _TargetMapping;
        public MappingVM TargetMapping { get => _TargetMapping; private set => this.RaiseAndSetIfChanged(ref _TargetMapping, value); }

        private bool _NewEntry;
        public bool NewEntry { get => _NewEntry; set => this.RaiseAndSetIfChanged(ref _NewEntry, value); }

        private readonly ObservableAsPropertyHelper<string> _SettingsTitle;
        public string SettingsTitle => _SettingsTitle.Value;

        public ICommand DiscardCommand { get; }

        public ICommand AcceptCommand { get; }

        public ICommand DeleteCommand { get; }

        public ICommand OpenBinaryPathDialogCommand { get; }

        public ICommand OpenFolderPathDialogCommand { get; }

        public Mapping DisplayedSettings { get; } = new Mapping();

        public MappingSettingsEditorVM(MainVM mainVM)
        {
            this.DiscardCommand = ReactiveCommand.Create(
                execute: this.Close,
                canExecute: this.WhenAny(x => x.NewEntry).Select(n => !n));
            this.AcceptCommand = ReactiveCommand.Create(
                execute: () =>
                {
                    Apply();
                    if (this.NewEntry)
                    {
                        mainVM.Settings.Mappings.Add(this.TargetMapping.Mapping);
                    }
                    Close();
                },
                canExecute: Observable.CombineLatest(
                    this.WhenAny(x => x.DisplayedSettings.BinaryPath),
                    this.WhenAny(x => x.DisplayedSettings.FolderPath),
                    (Binary, Folder) => (Binary, Folder))
                    .Select(t =>
                    {
                        if (string.IsNullOrWhiteSpace(t.Binary.Path)) return false;
                        if (string.IsNullOrWhiteSpace(t.Folder.Path)) return false;
                        return t.Binary.Exists || t.Folder.Exists;
                    }));
            this.DeleteCommand = ReactiveCommand.Create(
                execute: () =>
                {
                    if (!this.NewEntry)
                    {
                        mainVM.Settings.Mappings.Remove(this.TargetMapping.Mapping);
                    }
                    Close();
                });
            this.OpenBinaryPathDialogCommand = ReactiveCommand.Create(
                execute: () =>
                {
                    using (OpenFileDialog openFileDialog = new OpenFileDialog())
                    {
                        openFileDialog.RestoreDirectory = true;
                        if (openFileDialog.ShowDialog() == DialogResult.OK)
                        {
                            this.DisplayedSettings.BinaryPath = openFileDialog.FileName;
                        }
                    }
                });
            this.OpenFolderPathDialogCommand = ReactiveCommand.Create(
                execute: () =>
                {
                    using (var fbd = new FolderBrowserDialog())
                    {
                        fbd.SelectedPath = mainVM.Settings.LastReferencedDirectory;
                        DialogResult result = fbd.ShowDialog();

                        if (result == DialogResult.OK)
                        {
                            this.DisplayedSettings.FolderPath = fbd.SelectedPath;
                            mainVM.Settings.LastReferencedDirectory = fbd.SelectedPath;
                        }
                    }
                });
            this._SettingsTitle = this.WhenAny(x => x.NewEntry)
                .Select(newEntry =>
                {
                    if (newEntry)
                    {
                        return "Create New Mapping";
                    }
                    else
                    {
                        return "Modify Mapping Settings";
                    }
                })
                .ToProperty(this, nameof(SettingsTitle));
        }

        public void Target(MappingVM mapping, bool newItem)
        {
            this.NewEntry = newItem;
            this.TargetMapping = mapping;
            if (mapping == null) return;
            this.DisplayedSettings.CopyFieldsFrom(mapping.Mapping);
        }

        private void Close()
        {
            this.TargetMapping = null;
        }

        private void Apply()
        {
            this.TargetMapping?.Mapping.CopyFieldsFrom(this.DisplayedSettings);
        }
    }
}
