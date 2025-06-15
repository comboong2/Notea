using SP.ViewModels;

namespace SP.Modules.Daily.ViewModels
{
    public class DailyViewModel : ViewModelBase
    {
        public DailyHeaderViewModel HeaderViewModel { get; set; }
        public DailyBodyViewModel BodyViewModel { get; set; }

        public DailyViewModel(DateTime appStartDate)
        {
            HeaderViewModel = new DailyHeaderViewModel();
            BodyViewModel = new DailyBodyViewModel(appStartDate);
        }
    }
}
