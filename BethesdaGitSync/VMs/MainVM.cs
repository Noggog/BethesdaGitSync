using DynamicData;
using DynamicData.Binding;
using ReactiveUI;
using System;
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
        public ObservableCollectionExtended<MappingVM> Mappings { get; } = new ObservableCollectionExtended<MappingVM>();
        public SourceList<MappingVM> MappingsSource { get; } = new SourceList<MappingVM>();
        public ICommand AddCommand { get; }
        public ICommand HelpCommand { get; }

        public MainVM()
        {
            this.MappingsSource
                .Connect()
                .ObserveOn(RxApp.MainThreadScheduler)
                .Bind(this.Mappings)
                .DisposeMany()
                .Subscribe();
            this.AddCommand = ReactiveCommand.Create(
                execute: () => this.MappingsSource.Add(new MappingVM()));
            this.HelpCommand = ReactiveCommand.Create(
                execute: () => { });
        }
    }
}
