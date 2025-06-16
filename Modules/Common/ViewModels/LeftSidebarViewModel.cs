using SP.Modules.Common.Models;
using SP.ViewModels;
using System.Collections.ObjectModel;
using System.Windows.Controls;
using SP.Modules.Common.Views;
using SP.Modules.Subjects.Models;
using SP.Modules.Subjects.ViewModels;
using SP.Modules.Common.Helpers;

namespace SP.Modules.Common.ViewModels
{
    public class LeftSidebarViewModel : ViewModelBase
    {
        // 싱글톤 인스턴스 사용
        private readonly DatabaseHelper _db = DatabaseHelper.Instance;

        private string _sidebarTitle;
        public string SidebarTitle
        {
            get => _sidebarTitle;
            set
            {
                if (_sidebarTitle != value)
                {
                    _sidebarTitle = value;
                    OnPropertyChanged(nameof(SidebarTitle));
                }
            }
        }

        public ObservableCollection<SubjectGroupViewModel> Subjects { get; set; }

        private UserControl _sidebarContentView;
        public UserControl SidebarContentView
        {
            get => _sidebarContentView;
            set
            {
                if (_sidebarContentView != value)
                {
                    _sidebarContentView = value;
                    OnPropertyChanged(nameof(SidebarContentView));
                }
            }
        }

        public LeftSidebarViewModel() : this("main") { }

        public LeftSidebarViewModel(string context)
        {
            SidebarTitle = context == "main" ? "과목" : "오늘 할 일";
            Subjects = new ObservableCollection<SubjectGroupViewModel>();

            if (context == "main")
            {
                LoadSubjectsFromDatabase();
                SidebarContentView = new SubjectListView
                {
                    DataContext = this
                };
            }
            else // context == "today"
            {
                LoadSubjectsWithProgress();
                SidebarContentView = new SubjectProgressListView
                {
                    DataContext = this
                };
            }

            OnPropertyChanged(nameof(Subjects));
        }

        public void SetContext(string context)
        {
            SidebarTitle = context == "main" ? "과목" : "오늘 할 일";

            if (context == "main")
            {
                LoadSubjectsFromDatabase();
                SidebarContentView = new SubjectListView
                {
                    DataContext = this
                };
            }
            else // "today"
            {
                LoadSubjectsWithProgress();
                SidebarContentView = new SubjectProgressListView
                {
                    DataContext = this
                };
            }

            OnPropertyChanged(nameof(SidebarTitle));
            OnPropertyChanged(nameof(Subjects));
            OnPropertyChanged(nameof(SidebarContentView));
        }

        // ✅ 실제 DB에서 과목 로드 (더미 데이터 제거)
        private void LoadSubjectsFromDatabase()
        {
            Subjects.Clear();
            var subjectList = _db.LoadSubjectsWithGroups();
            foreach (var subject in subjectList)
            {
                Subjects.Add(subject);
            }
        }

        // ✅ 실제 DB에서 진행률과 함께 과목 로드 (더미 데이터 제거)
        private void LoadSubjectsWithProgress()
        {
            Subjects.Clear();

            // 전체 학습시간 계산 및 설정
            int totalAllSubjectsTime = _db.GetTotalAllSubjectsStudyTime();
            SubjectGroupViewModel.SetGlobalTotalTime(totalAllSubjectsTime);

            var subjectList = _db.LoadSubjectsWithStudyTime();
            foreach (var subject in subjectList)
            {
                Subjects.Add(subject);
            }
        }

        // 데이터 새로고침 메서드
        public void RefreshData()
        {
            if (SidebarTitle == "과목")
            {
                LoadSubjectsFromDatabase();
            }
            else
            {
                LoadSubjectsWithProgress();
            }
        }
    }
}