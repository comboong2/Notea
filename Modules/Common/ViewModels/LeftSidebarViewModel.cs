﻿using SP.Modules.Common.Models;
using SP.ViewModels;
using System.Collections.ObjectModel;
using System.Windows.Controls;
using SP.Modules.Common.Views;
using SP.Modules.Subjects.Models;
using SP.Modules.Subjects.ViewModels;
using SP.Modules.Common.Helpers;
using SP.Modules.Daily.ViewModels;

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

        // 기본 과목 데이터 (과목 리스트용)
        public ObservableCollection<SubjectGroupViewModel> SubjectGroups { get; set; }

        // 공유 Progress 데이터 (과목페이지 우측 패널용)
        private ObservableCollection<SubjectProgressViewModel> _sharedSubjectProgress;
        public ObservableCollection<SubjectProgressViewModel> SharedSubjectProgress
        {
            get => _sharedSubjectProgress;
            set
            {
                _sharedSubjectProgress = value;
                OnPropertyChanged(nameof(SharedSubjectProgress));
                OnPropertyChanged(nameof(Subjects)); // Subjects도 업데이트
            }
        }

        // 동적 데이터 소스 - 컨텍스트에 따라 다른 타입 반환
        public object Subjects
        {
            get
            {
                // today 컨텍스트이고 공유 데이터가 있으면 SharedSubjectProgress 반환
                if (_currentContext == "today" && SharedSubjectProgress != null)
                {
                    return SharedSubjectProgress;
                }
                // 기본적으로는 SubjectGroups 반환
                return SubjectGroups;
            }
        }

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

        // 현재 컨텍스트 저장
        private string _currentContext;

        public LeftSidebarViewModel() : this("main") { }

        public LeftSidebarViewModel(string context)
        {
            _currentContext = context;
            SidebarTitle = context == "main" ? "과목" : "오늘 할 일";
            SubjectGroups = new ObservableCollection<SubjectGroupViewModel>();

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
                // 빈 상태로 시작 (나중에 공유 데이터가 설정됨)
                SidebarContentView = new SubjectProgressListView
                {
                    DataContext = this
                };
            }

            OnPropertyChanged(nameof(Subjects));
        }

        public void SetContext(string context)
        {
            _currentContext = context;
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
                SidebarContentView = new SubjectProgressListView
                {
                    DataContext = this
                };
            }

            OnPropertyChanged(nameof(SidebarTitle));
            OnPropertyChanged(nameof(Subjects));
            OnPropertyChanged(nameof(SidebarContentView));
        }

        // 공유 Progress 데이터 설정
        public void SetSharedSubjectProgress(ObservableCollection<SubjectProgressViewModel> sharedProgress)
        {
            SharedSubjectProgress = sharedProgress;
            System.Diagnostics.Debug.WriteLine($"[LeftSidebarViewModel] 공유 데이터 설정됨: {sharedProgress?.Count ?? 0}개 항목");
        }

        // 실제 DB에서 과목 로드
        private void LoadSubjectsFromDatabase()
        {
            SubjectGroups.Clear();
            var subjectList = _db.LoadSubjectsWithGroups();
            foreach (var subject in subjectList)
            {
                SubjectGroups.Add(subject);
            }
            OnPropertyChanged(nameof(Subjects));
        }

        // 데이터 새로고침 메서드
        public void RefreshData()
        {
            if (SidebarTitle == "과목")
            {
                LoadSubjectsFromDatabase();
            }
        }
    }
}