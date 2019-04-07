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
    public class MappingVM : ReactiveObject
    {
        public Mapping Mapping { get; }

        private readonly ObservableAsPropertyHelper<string> _Nickname;
        public string Nickname => _Nickname.Value;

        private readonly ObservableAsPropertyHelper<bool> _BinaryPathValid;
        public bool BinaryPathValid => _BinaryPathValid.Value;

        private readonly ObservableAsPropertyHelper<bool> _FolderPathValid;
        public bool FolderPathValid => _FolderPathValid.Value;
        
        public ICommand OpenSettingsCommand { get; }

        public MappingVM(MainVM mainVM, Mapping mapping)
        {
            this.Mapping = mapping;

            this.OpenSettingsCommand = ReactiveCommand.Create(
                execute: () =>
                {
                    mainVM.MappingEditorVM.Target(
                        this,
                        newItem: false);
                });

            this._Nickname = Observable.CombineLatest(
                    this.WhenAny(x => x.Mapping.Nickname),
                    this.WhenAny(x => x.Mapping.BinaryPath)
                        .Select(binary => binary.Name),
                    (nickname, binary) => string.IsNullOrWhiteSpace(nickname) ? binary : nickname)
                .ToProperty(this, nameof(Nickname));

            this._BinaryPathValid = Observable.Merge(
                this.WhenAny(x => x.Mapping.BinaryPath),
                Observable.Interval(TimeSpan.FromSeconds(1))
                    .WithLatestFrom(
                        this.WhenAny(x => x.Mapping.BinaryPath),
                        resultSelector: (timer, bin) => bin))
                .Select(bin => bin.Exists)
                .ToProperty(this, nameof(BinaryPathValid));

            this._FolderPathValid = Observable.Merge(
                this.WhenAny(x => x.Mapping.FolderPath),
                Observable.Interval(TimeSpan.FromSeconds(1))
                    .WithLatestFrom(
                        this.WhenAny(x => x.Mapping.FolderPath),
                        resultSelector: (timer, bin) => bin))
                .Select(bin => bin.Exists)
                .ToProperty(this, nameof(FolderPathValid));
        }
    }
}
