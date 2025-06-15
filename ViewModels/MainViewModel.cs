using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using SP.Modules.Common.ViewModels;
using SP.Modules.Common.Views;
using SP.Modules.Daily.ViewModels;
using SP.Modules.Daily.Views;
using SP.Modules.Subjects.Views;

namespace SP.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private object _currentView;
        public object CurrentView
        {
            get => _currentView;
            set
            {
                if (_currentView != value)
                {
                    _currentView = value;
                    OnPropertyChanged(nameof(CurrentView));
                }
            }
        }

        public DateTime AppStartDate { get; } = DateTime.Now.Date;

        //  ViewModel과 View를 한 번만 생성
        private readonly DailyBodyViewModel DailyBodyVM;
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

        //  생성자
        public MainViewModel()
        {
            SidebarViewModel = new LeftSidebarViewModel("main");

            // ViewModel 생성
            DailyBodyVM = new DailyBodyViewModel(AppStartDate);

            // View 생성 (한 번만)
            _dailyHeaderView = new DailyHeaderView();
            _dailyBodyView = new DailyBodyView
            {
                DataContext = DailyBodyVM
            };

            _subjectHeaderView = new SubjectListPageHeaderView();
            _subjectBodyView = new SubjectListPageBodyView();

            // 초기 화면
            HeaderContent = _dailyHeaderView;
            BodyContent = _dailyBodyView;

            // 사이드바 명령
            ToggleSidebarCommand = new RelayCommand(ToggleSidebar);
            ExpandSidebarCommand = new RelayCommand(() => LeftSidebarWidth = new GridLength(280));

            // 페이지 전환 명령
            NavigateToTodayCommand = new RelayCommand(() =>
            {
                HeaderContent = _dailyHeaderView;
                BodyContent = _dailyBodyView;
                SidebarViewModel.SetContext("main");

                // 날짜 기반 데이터 로드
                DailyBodyVM.LoadDailyData(AppStartDate);
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
            DailyBodyVM.LoadDailyData(date);
        }

        private void ToggleSidebar()
        {
            LeftSidebarWidth = LeftSidebarWidth.Value == 0
                ? new GridLength(280)
                : new GridLength(0);
        }

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

        // INotifyPropertyChanged
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
