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

        public GroupingVM(Grouping grouping)
        {
            this.Settings = grouping;
            this.SelectedMappings = grouping.Mappings
                .Connect()
                .ObserveOn(RxApp.MainThreadScheduler)
                .Transform(m => new MappingVM(m))
                .DisposeMany()
                // Bind to visible mappings for GUI
                .Bind(this._Mappings)
                // Filter ones that are selected, and save to collection
                .AutoRefresh(m => m.IsSelected)
                .Filter(m => m.IsSelected)
                .AsObservableList();
        }
    }
}
