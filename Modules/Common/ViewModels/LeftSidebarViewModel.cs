using SP.Modules.Common.Models;
using SP.ViewModels;
using System.Collections.ObjectModel;
using System.Windows.Controls;
using SP.Modules.Common.Views;
using SP.Modules.Subjects.Models;
using SP.Modules.Subjects.ViewModels;

namespace SP.Modules.Common.ViewModels
{
    public class LeftSidebarViewModel : ViewModelBase
    {
        private string _sidebarTitle;
        public string SidebarTitle
        {
            get => _sidebarTitle;
            set
            {
                if (_sidebarTitle != value)
                {
                    _sidebarTitle = value;
                    OnPropertyChanged(nameof(SidebarTitle)); // 반드시 호출
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
                    OnPropertyChanged(nameof(SidebarContentView)); // ← 이것이 핵심
                }
            }
        }


        public LeftSidebarViewModel() : this("main") { }

        public LeftSidebarViewModel(string context)
        {
            SidebarTitle = context == "main" ? "과목" : "오늘 할 일";

            if (context == "main")
            {
                Subjects = new ObservableCollection<SubjectGroupViewModel>
                {
                    new SubjectGroupViewModel
                    {
                        SubjectName = "자료구조",
                        TopicGroups = new ObservableCollection<TopicGroupViewModel>
                        {
                            new TopicGroupViewModel
                            {
                                GroupTitle = "기술"
                            }
                        }
                    },
                    new SubjectGroupViewModel
                    {
                        SubjectName = "인공지능",
                        TopicGroups = new ObservableCollection<TopicGroupViewModel>
                        {
                            new TopicGroupViewModel
                            {
                                GroupTitle = "가이드"
                            }
                        }
                    }
                };

                SidebarContentView = new SubjectListView
                {
                    DataContext = this //  뷰와 뷰모델 연결
                };
            }
            else // context == "today"
            {
                Subjects = new ObservableCollection<SubjectGroupViewModel>
                {
                    new SubjectGroupViewModel
                    {
                        SubjectName = "과목1",
                        TopicGroups = new ObservableCollection<TopicGroupViewModel>
                        {
                            new TopicGroupViewModel
                            {
                                Topics = new ObservableCollection<TopicItem>
                                {
                                    new TopicItem { Name = "분류1", Progress = 0.6 },
                                    new TopicItem { Name = "분류2", Progress = 0.3 }
                                }
                            }
                        }
                    },
                    new SubjectGroupViewModel
                    {
                        SubjectName = "과목2",
                        TopicGroups = new ObservableCollection<TopicGroupViewModel>
                        {
                            new TopicGroupViewModel
                            {
                                Topics = new ObservableCollection<TopicItem>
                                {
                                    new TopicItem { Name = "분류A", Progress = 0.8 }
                                }
                            }
                        }
                    }
                };

                SidebarContentView = new SubjectProgressListView
                {
                    DataContext = this //  뷰와 뷰모델 연결
                };
            }
            // Notify Subjects 변경
            OnPropertyChanged(nameof(Subjects));
        }
        public void SetContext(string context)
        {
            SidebarTitle = context == "main" ? "과목" : "오늘 할 일";

            if (context == "main")
            {
                Subjects = new ObservableCollection<SubjectGroupViewModel>
        {
            new SubjectGroupViewModel
            {
                SubjectName = "자료구조",
                TopicGroups = new ObservableCollection<TopicGroupViewModel>
                {
                    new TopicGroupViewModel { GroupTitle = "기술" }
                }
            },
            new SubjectGroupViewModel
            {
                SubjectName = "인공지능",
                TopicGroups = new ObservableCollection<TopicGroupViewModel>
                {
                    new TopicGroupViewModel { GroupTitle = "가이드" }
                }
            }
        };

                SidebarContentView = new SubjectListView
                {
                    DataContext = this
                };
            }
            else // "today"
            {
                Subjects = new ObservableCollection<SubjectGroupViewModel>
        {
            new SubjectGroupViewModel
            {
                SubjectName = "과목1",
                TopicGroups = new ObservableCollection<TopicGroupViewModel>
                {
                    new TopicGroupViewModel
                    {
                        Topics = new ObservableCollection<TopicItem>
                        {
                            new TopicItem { Name = "분류1", Progress = 0.6 },
                            new TopicItem { Name = "분류2", Progress = 0.3 }
                        }
                    }
                }
            },
            new SubjectGroupViewModel
            {
                SubjectName = "과목2",
                TopicGroups = new ObservableCollection<TopicGroupViewModel>
                {
                    new TopicGroupViewModel
                    {
                        Topics = new ObservableCollection<TopicItem>
                        {
                            new TopicItem { Name = "분류A", Progress = 0.8 }
                        }
                    }
                }
            }
        };

                SidebarContentView = new SubjectProgressListView
                {
                    DataContext = this
                };
            }

            // ✅ 변경 사항을 UI에 반영하려면 반드시 호출!
            OnPropertyChanged(nameof(SidebarTitle));
            OnPropertyChanged(nameof(Subjects));
            OnPropertyChanged(nameof(SidebarContentView));
        }

    }
}
