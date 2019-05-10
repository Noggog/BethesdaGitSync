using DynamicData;
using DynamicData.Binding;
using Noggog.WPF;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BethesdaGitSync
{
    public class GroupingVM : ViewModel
    {
        public Grouping Settings { get; }
        public IObservableList<MappingVM> SelectedMappings { get; }
        private readonly ObservableCollectionExtended<MappingVM> _MappingsView = new ObservableCollectionExtended<MappingVM>();
        public IObservableCollection<MappingVM> MappingsView => _MappingsView;
        public IObservableList<MappingVM> Mappings { get; }

        private readonly ObservableAsPropertyHelper<bool> _Empty;
        public bool Empty => _Empty.Value;

        public GroupingVM(Grouping grouping)
        {
            this.Settings = grouping;
            this.Mappings = grouping.Mappings
                .Connect()
                .ObserveOn(RxApp.MainThreadScheduler)
                .Transform(m => new MappingVM(m))
                .DisposeMany()
                // Bind to visible mappings for GUI
                .Bind(_MappingsView)
                .AsObservableList();
            SelectedMappings = this.Mappings.Connect()
                // Filter ones that are selected, and save to collection
                .AutoRefresh(m => m.IsSelected)
                .Filter(m => m.IsSelected)
                .AsObservableList();

            // Select first mapping (if any) when any are removed
            this.Settings.Mappings.Connect()
                .WhereReasonsAre(ListChangeReason.Remove, ListChangeReason.RemoveRange)
                .Unit()
                // And once while starting up
                .StartWith(Unit.Default)
                .ObserveOn(RxApp.MainThreadScheduler)
                .WithLatestFrom(
                    this.Mappings.Connect().Top(1).QueryWhenChanged()
                        .Select(q => q.FirstOrDefault())
                        .StartWith(this.Mappings.FirstOrDefault()),
                    resultSelector: (remove, top) => top)
                .NotNull()
                .Subscribe(top => top.IsSelected = true)
                .DisposeWith(this.CompositeDisposable);

            this._Empty = this.Settings.Mappings.Connect()
                .CollectionCount()
                .Select(c => c == 0)
                .ToProperty(this, nameof(Empty));
        }
    }
}
