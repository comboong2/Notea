using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using SP.Modules.Common.ViewModels;
using SP.Modules.Common.Views;
using SP.Modules.Daily.ViewModels;
using SP.Modules.Daily.Views;
using SP.Modules.Subjects.Views;
using SP.Modules.Subjects.ViewModels;

namespace SP.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        public DateTime AppStartDate { get; } = DateTime.Now.Date;

        // ViewModel들 (한 번만 생성)
        private readonly DailyHeaderViewModel _dailyHeaderVM;
        private readonly DailyBodyViewModel _dailyBodyVM;
        private readonly SubjectListPageViewModel _subjectListPageVM;

        // View들 (한 번만 생성)
        private readonly DailyHeaderView _dailyHeaderView;
        private readonly DailyBodyView _dailyBodyView;
        private readonly SubjectListPageHeaderView _subjectHeaderView;
        private readonly SubjectListPageBodyView _subjectBodyView;

        private LeftSidebarViewModel _sidebarViewModel;
        public LeftSidebarViewModel SidebarViewModel
        {
            get => _sidebarViewModel;
            set
            {
                if (_sidebarViewModel != value)
                {
                    _sidebarViewModel = value;
                    OnPropertyChanged(nameof(SidebarViewModel));
                }
            }
        }

        private GridLength _leftSidebarWidth = new GridLength(280);
        public GridLength LeftSidebarWidth
        {
            get => _leftSidebarWidth;
            set
            {
                if (_leftSidebarWidth != value)
                {
                    _leftSidebarWidth = value;
                    OnPropertyChanged(nameof(LeftSidebarWidth));
                    OnPropertyChanged(nameof(IsSidebarCollapsed));
                }
            }
        }

        public bool IsSidebarCollapsed => LeftSidebarWidth.Value == 0;

        public ICommand ToggleSidebarCommand { get; }
        public ICommand ExpandSidebarCommand { get; }
        public ICommand NavigateToSubjectListCommand { get; }
        public ICommand NavigateToTodayCommand { get; }

        // 헤더/본문 컨텐츠 프로퍼티
        private object _headerContent;
        public object HeaderContent
        {
            get => _headerContent;
            set
            {
                if (_headerContent != value)
                {
                    _headerContent = value;
                    OnPropertyChanged(nameof(HeaderContent));
                }
            }
        }

        private object _bodyContent;
        public object BodyContent
        {
            get => _bodyContent;
            set
            {
                if (_bodyContent != value)
                {
                    _bodyContent = value;
                    OnPropertyChanged(nameof(BodyContent));
                }
            }
        }

        public MainViewModel()
        {
            // 사이드바 ViewModel 초기화
            SidebarViewModel = new LeftSidebarViewModel("main");

            // ViewModel들 생성 (한 번만)
            _dailyHeaderVM = new DailyHeaderViewModel();
            _dailyBodyVM = new DailyBodyViewModel(AppStartDate);
            _subjectListPageVM = new SubjectListPageViewModel();

            // View들 생성 및 DataContext 설정 (한 번만)
            _dailyHeaderView = new DailyHeaderView { DataContext = _dailyHeaderVM };
            _dailyBodyView = new DailyBodyView { DataContext = _dailyBodyVM };
            _subjectHeaderView = new SubjectListPageHeaderView();
            _subjectBodyView = new SubjectListPageBodyView { DataContext = _subjectListPageVM };

            // 초기 화면 설정 (Daily 화면)
            HeaderContent = _dailyHeaderView;
            BodyContent = _dailyBodyView;

            // Commands 초기화
            ToggleSidebarCommand = new RelayCommand(ToggleSidebar);
            ExpandSidebarCommand = new RelayCommand(() => LeftSidebarWidth = new GridLength(280));

            NavigateToTodayCommand = new RelayCommand(() =>
            {
                HeaderContent = _dailyHeaderView;
                BodyContent = _dailyBodyView;
                SidebarViewModel.SetContext("main");

                // 현재 날짜로 데이터 로드
                _dailyBodyVM.LoadDailyData(AppStartDate);
            });

            NavigateToSubjectListCommand = new RelayCommand(() =>
            {
                HeaderContent = _subjectHeaderView;
                BodyContent = _subjectBodyView;
                SidebarViewModel.SetContext("today");
            });
        }

        public void OnDateSelected(DateTime date)
        {
            _dailyBodyVM.LoadDailyData(date);
        }

        private void ToggleSidebar()
        {
            LeftSidebarWidth = LeftSidebarWidth.Value == 0
                ? new GridLength(280)
                : new GridLength(0);
        }

        // INotifyPropertyChanged 구현
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}