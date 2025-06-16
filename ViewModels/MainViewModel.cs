using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
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

        // 🆕 공유 데이터 소스 - 두 페이지에서 모두 사용
        public ObservableCollection<SubjectProgressViewModel> SharedSubjectProgress { get; set; }

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
            // 🆕 공유 데이터 소스 초기화
            SharedSubjectProgress = new ObservableCollection<SubjectProgressViewModel>();

            // 사이드바 ViewModel 초기화
            SidebarViewModel = new LeftSidebarViewModel("main");

            // ViewModel들 생성 (한 번만)
            _dailyHeaderVM = new DailyHeaderViewModel();
            _dailyBodyVM = new DailyBodyViewModel(AppStartDate);
            _subjectListPageVM = new SubjectListPageViewModel();

            // 🆕 DailyBodyViewModel의 Subjects를 공유 데이터로 교체
            _dailyBodyVM.SetSharedSubjects(SharedSubjectProgress);

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

                // 과목 페이지로 전환할 때 사이드바 컨텍스트 변경
                SidebarViewModel.SetContext("today");

                // 공유 데이터 설정
                SidebarViewModel.SetSharedSubjectProgress(SharedSubjectProgress);

                System.Diagnostics.Debug.WriteLine($"[MainViewModel] 과목페이지로 전환 - 공유 데이터 항목 수: {SharedSubjectProgress.Count}");
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