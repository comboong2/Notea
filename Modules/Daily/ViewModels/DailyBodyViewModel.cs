using System;
using System.Collections.ObjectModel;
using System.Windows.Input;
using SP.Modules.Common.Helpers;
using SP.Modules.Daily.Models;
using SP.ViewModels;

namespace SP.Modules.Daily.ViewModels
{
    public class DailyBodyViewModel : ViewModelBase
    {
        // 과목 리스트
        public ObservableCollection<SubjectProgressViewModel> Subjects { get; set; }

        // TODO 리스트
        public ObservableCollection<TodoItem> TodoList { get; set; }

<<<<<<< Updated upstream
        private readonly DatabaseHelper _db = new();
=======
        // 싱글톤 인스턴스 사용
        private readonly DatabaseHelper _db = DatabaseHelper.Instance;
>>>>>>> Stashed changes

        // 새 할 일 텍스트
        private string _newTodoText;
        public string NewTodoText
        {
            get => _newTodoText;
            set => SetProperty(ref _newTodoText, value);
        }

        // 입력 모드 여부
        private bool _isAdding = false;
        public bool IsAdding
        {
            get => _isAdding;
            set => SetProperty(ref _isAdding, value);
        }

        // 포커스 요청용 이벤트 (View에서 연결)
        public Action? RequestFocusOnInput { get; set; }

        public ICommand AddTodoCommand { get; }
        public ICommand StartAddCommand { get; }

        public DailyBodyViewModel(DateTime appStartDate)
        {
            SelectedDate = appStartDate;

<<<<<<< Updated upstream
            Subjects = new ObservableCollection<SubjectProgressViewModel>
            {
                new SubjectProgressViewModel { SubjectName = "자료구조", Progress = 0.4 },
                new SubjectProgressViewModel { SubjectName = "인공지능", Progress = 0.7 }
            };

=======
            Subjects = new ObservableCollection<SubjectProgressViewModel>();
>>>>>>> Stashed changes
            TodoList = new ObservableCollection<TodoItem>();

            AddTodoCommand = new RelayCommand(AddTodo);
            StartAddCommand = new RelayCommand(() =>
            {
                IsAdding = true;
                RequestFocusOnInput?.Invoke();
            });

<<<<<<< Updated upstream
            // comment,TodoList 불러오기
            LoadDailyData(SelectedDate);
=======
            // 실제 데이터 로드
            LoadDailyData(SelectedDate);
            LoadSubjectsProgress();
>>>>>>> Stashed changes
        }

        private void AddTodo()
        {
            if (!string.IsNullOrWhiteSpace(NewTodoText))
            {
                string trimmed = NewTodoText.Trim();
                int id = _db.AddTodo(SelectedDate, trimmed); // DB에 저장 + ID 받기

                var newItem = new TodoItem
                {
                    Id = id,
                    Title = trimmed,
                    IsCompleted = false
                };

                TodoList.Add(newItem);
                NewTodoText = string.Empty;
            }
            IsAdding = false;
        }

<<<<<<< Updated upstream

=======
>>>>>>> Stashed changes
        // 헤더 하단의 comment, d-day 관련
        private string _comment = string.Empty;
        public string Comment
        {
            get => _comment;
            set
            {
                if (SetProperty(ref _comment, value))
                {
                    _db.SaveOrUpdateComment(SelectedDate, _comment); // 저장
                }
            }
        }

        public void LoadDailyData(DateTime date)
        {
            SelectedDate = date;

            System.Diagnostics.Debug.WriteLine($"[DailyBodyViewModel] LoadDailyData 호출됨. 날짜: {date.ToShortDateString()}"); // ★★★ 디버그 출력 추가 ★★★

            // Comment 불러오기
            Comment = _db.GetCommentByDate(date);
            System.Diagnostics.Debug.WriteLine($"[DailyBodyViewModel] Comment 로드됨: '{Comment}'"); // ★★★ 디버그 출력 추가 ★★★

            var todos = _db.GetTodosByDate(date);
            TodoList.Clear();
            System.Diagnostics.Debug.WriteLine($"[DailyBodyViewModel] 투두 항목 {todos.Count}개 DB에서 로드됨."); // ★★★ 디버그 출력 추가 ★★★

            foreach (var todo in todos)
            {
                //  IsCompleted 변경될 때마다 DB 반영
                todo.PropertyChanged += (s, e) =>
                {
                    if (e.PropertyName == nameof(TodoItem.IsCompleted))
                    {
                        _db.UpdateTodoCompletion(todo.Id, todo.IsCompleted);
                    }
                };

                TodoList.Add(todo);
            }
<<<<<<< Updated upstream
            System.Diagnostics.Debug.WriteLine($"[DailyBodyViewModel] TodoList에 {TodoList.Count}개 항목 추가됨."); // ★★★ 디버그 출력 추가 ★★★
        }

=======
            System.Diagnostics.Debug.WriteLine($"[DailyBodyViewModel] TodoList에 {TodoList.Count}개 항목 추가됨.");
        }

        // ✅ 실제 DB에서 과목 진행률 로드 (더미 데이터 제거)
        private void LoadSubjectsProgress()
        {
            Subjects.Clear();

            // 전체 학습시간 계산
            int totalAllSubjectsTime = _db.GetTotalAllSubjectsStudyTime();

            var subjectList = _db.LoadSubjectsWithStudyTime();
            foreach (var subject in subjectList)
            {
                // SubjectGroupViewModel을 SubjectProgressViewModel으로 변환
                var progressVM = new SubjectProgressViewModel
                {
                    SubjectName = subject.SubjectName,
                    Progress = totalAllSubjectsTime > 0 ? (double)subject.TotalStudyTime / totalAllSubjectsTime : 0.0
                };

                Subjects.Add(progressVM);
            }

            System.Diagnostics.Debug.WriteLine($"[DailyBodyViewModel] 과목 진행률 {Subjects.Count}개 로드됨.");
        }
>>>>>>> Stashed changes

        public string InfoTitle => IsToday ? "시험" : "총 학습 시간";
        public string InfoContent => IsToday ? $"D-{Dday}" : TotalStudyTime;

        public bool IsToday => SelectedDate.Date == DateTime.Today;
        
        private DateTime _selectedDate;
        public DateTime SelectedDate
        {
            get => _selectedDate;
            set
            {
                if (SetProperty(ref _selectedDate, value))
                {
                    // 날짜가 바뀌면 Comment를 다시 불러오고, 할 일도 갱신하도록 처리
                    Comment = _db.GetCommentByDate(value);
                    // TodoList = new ObservableCollection<TodoItem>(_db.GetTodosByDate(value)); // 추후 확장
                }
            }
        }

        public TimeSpan StudyTime { get; set; } = TimeSpan.FromMinutes(165);
        public int Dday => (TargetDate - DateTime.Today).Days;
        public DateTime TargetDate { get; set; } = new DateTime(2025, 6, 22);

        // ✅ 실제 DB에서 총 학습시간 계산 (더미 데이터 제거)
        public string TotalStudyTime
        {
            get
            {
                int totalSeconds = _db.GetTotalAllSubjectsStudyTime();
                TimeSpan totalTime = TimeSpan.FromSeconds(totalSeconds);
                return $"{(int)totalTime.TotalHours}시간 {totalTime.Minutes}분";
            }
        }

        // ✅ 누락된 메서드들 추가 (올바른 시그니처)

        // 과목을 안전하게 추가하는 메서드 - 여러 오버로드
        public void AddSubjectSafely(string subjectName)
        {
            try
            {
                int subjectId = _db.AddSubject(subjectName);

                // UI 업데이트
                LoadSubjectsProgress();

                System.Diagnostics.Debug.WriteLine($"[DailyBodyViewModel] 과목 '{subjectName}' 추가됨. ID: {subjectId}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DailyBodyViewModel] 과목 추가 오류: {ex.Message}");
            }
        }

        // SubjectProgressViewModel을 받는 오버로드
        public void AddSubjectSafely(SubjectProgressViewModel subject)
        {
            if (subject != null && !string.IsNullOrWhiteSpace(subject.SubjectName))
            {
                AddSubjectSafely(subject.SubjectName);
            }
        }

        // 할 일 항목 삭제
        public void DeleteTodoItem(TodoItem todo)
        {
            if (todo == null) return;

            try
            {
                _db.DeleteTodo(todo.Id);
                TodoList.Remove(todo);

                System.Diagnostics.Debug.WriteLine($"[DailyBodyViewModel] 할 일 '{todo.Title}' 삭제됨.");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DailyBodyViewModel] 할 일 삭제 오류: {ex.Message}");
            }
        }

        // 데이터 새로고침 메서드
        public void RefreshSubjectsProgress()
        {
            LoadSubjectsProgress();
        }
    }
}
