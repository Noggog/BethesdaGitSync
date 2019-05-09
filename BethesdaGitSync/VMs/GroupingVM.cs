using DynamicData;
using DynamicData.Binding;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BethesdaGitSync
{
    public class GroupingVM : ReactiveObject
    {
        public Grouping Settings { get; }
        public IObservableList<MappingVM> SelectedMappings { get; }
        private ObservableCollectionExtended<MappingVM> _Mappings { get; } = new ObservableCollectionExtended<MappingVM>();
        public IObservableCollection<MappingVM> Mappings => _Mappings;

        private readonly ObservableAsPropertyHelper<bool> _Empty;
        public bool Empty => _Empty.Value;

        public GroupingVM(Grouping grouping)
        {
            bool first = true;
            this.Settings = grouping;
            this.SelectedMappings = grouping.Mappings
                .Connect()
                .ObserveOn(RxApp.MainThreadScheduler)
                .Transform(m =>
                {
                    var ret = new MappingVM(m)
                    {
                        IsSelected = first
                    };
                    first = false;
                    return ret;
                })
                .DisposeMany()
                // Bind to visible mappings for GUI
                .Bind(this._Mappings)
                // Filter ones that are selected, and save to collection
                .AutoRefresh(m => m.IsSelected)
                .Filter(m => m.IsSelected)
                .AsObservableList();

            this._Empty = this.Settings.Mappings.Connect()
                .CollectionCount()
                .Select(c => c == 0)
                .ToProperty(this, nameof(Empty));
        }
    }
}
