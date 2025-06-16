using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Windows.Input;
using SP.Modules.Common.Helpers;
using SP.Modules.Daily.Models;
using SP.Modules.Daily.ViewModels;
using SP.Modules.Subjects.ViewModels;
using SP.ViewModels;

namespace SP.Modules.Daily.ViewModels
{
    public class DailyBodyViewModel : ViewModelBase
    {
        // 과목 리스트 - 공유 데이터 또는 자체 데이터
        private ObservableCollection<SubjectProgressViewModel> _subjects;
        public ObservableCollection<SubjectProgressViewModel> Subjects
        {
            get => _subjects;
            set
            {
                if (_subjects != null)
                {
                    _subjects.CollectionChanged -= Subjects_CollectionChanged;
                }

                _subjects = value;

                if (_subjects != null)
                {
                    _subjects.CollectionChanged += Subjects_CollectionChanged;
                }

                OnPropertyChanged(nameof(Subjects));
            }
        }

        // TODO 리스트
        public ObservableCollection<TodoItem> TodoList { get; set; }

        // 싱글톤 DB 헬퍼 사용
        private readonly DatabaseHelper _db = DatabaseHelper.Instance;

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
        public ICommand DeleteTodoCommand { get; }

        // 무한 루프 방지 플래그
        private bool _isLoadingSubjects = false;

        public DailyBodyViewModel(DateTime appStartDate)
        {
            SelectedDate = appStartDate;

            // 기본 컬렉션으로 시작 (나중에 공유 데이터로 교체됨)
            Subjects = new ObservableCollection<SubjectProgressViewModel>();
            TodoList = new ObservableCollection<TodoItem>();

            AddTodoCommand = new RelayCommand(AddTodo);
            StartAddCommand = new RelayCommand(() =>
            {
                IsAdding = true;
                RequestFocusOnInput?.Invoke();
            });
            DeleteTodoCommand = new RelayCommand<TodoItem>(DeleteTodo);

            // comment, TodoList, DailySubjects 불러오기
            LoadDailyData(SelectedDate);
        }

        // 공유 데이터 설정 메소드
        public void SetSharedSubjects(ObservableCollection<SubjectProgressViewModel> sharedSubjects)
        {
            Subjects = sharedSubjects;
            System.Diagnostics.Debug.WriteLine("[DailyBodyViewModel] 공유 데이터로 전환됨");
        }

        private void Subjects_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            // 무한 루프 방지를 위한 플래그 체크
            if (_isLoadingSubjects) return;

            SaveDailySubjects();
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

                // PropertyChanged 이벤트 구독
                newItem.PropertyChanged += (s, e) =>
                {
                    if (e.PropertyName == nameof(TodoItem.IsCompleted))
                    {
                        _db.UpdateTodoCompletion(newItem.Id, newItem.IsCompleted);
                    }
                };

                TodoList.Add(newItem);
                NewTodoText = string.Empty;
            }
            IsAdding = false;
        }

        private void DeleteTodo(TodoItem todo)
        {
            if (todo == null)
            {
                System.Diagnostics.Debug.WriteLine("[Todo] 삭제할 Todo가 null입니다.");
                return;
            }

            System.Diagnostics.Debug.WriteLine($"[Todo] Todo 삭제 시도: {todo.Title} (ID: {todo.Id})");

            try
            {
                _db.DeleteTodo(todo.Id);
                TodoList.Remove(todo);
                System.Diagnostics.Debug.WriteLine($"[Todo] Todo 삭제 완료: {todo.Title}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Todo] Todo 삭제 오류: {ex.Message}");
            }
        }

        // DailyBodyView.xaml.cs에서 호출할 수 있도록 public으로 변경
        public void DeleteTodoItem(TodoItem todo)
        {
            DeleteTodo(todo);
        }

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
            // 같은 날짜면 다시 로딩하지 않음 (중복 방지)
            if (SelectedDate.Date == date.Date && Subjects.Count > 0)
            {
                System.Diagnostics.Debug.WriteLine($"[DailyBodyViewModel] 같은 날짜 데이터 이미 로드됨. 스킵.");
                return;
            }

            SelectedDate = date;

            System.Diagnostics.Debug.WriteLine($"[DailyBodyViewModel] LoadDailyData 호출됨. 날짜: {date.ToShortDateString()}");

            // Comment 불러오기
            Comment = _db.GetCommentByDate(date);
            System.Diagnostics.Debug.WriteLine($"[DailyBodyViewModel] Comment 로드됨: '{Comment}'");

            // TodoList 불러오기
            var todos = _db.GetTodosByDate(date);
            TodoList.Clear();
            System.Diagnostics.Debug.WriteLine($"[DailyBodyViewModel] 투두 항목 {todos.Count}개 DB에서 로드됨.");

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
            System.Diagnostics.Debug.WriteLine($"[DailyBodyViewModel] TodoList에 {TodoList.Count}개 항목 추가됨.");

            // 오늘 할 일 과목 리스트 불러오기
            LoadDailySubjects(date);
        }

        private void LoadDailySubjects(DateTime date)
        {
            _isLoadingSubjects = true; // 무한 루프 방지 플래그 설정

            try
            {
                var dailySubjects = _db.GetDailySubjects(date);

                // 기존 데이터와 비교하여 중복 방지
                var existingSubjects = Subjects.ToList();
                Subjects.Clear();

                foreach (var (subjectName, progress, studyTimeMinutes) in dailySubjects)
                {
                    // 중복 체크: 같은 이름의 과목이 이미 있는지 확인
                    if (!existingSubjects.Any(s => s.SubjectName.Equals(subjectName, StringComparison.OrdinalIgnoreCase)))
                    {
                        Subjects.Add(new SubjectProgressViewModel
                        {
                            SubjectName = subjectName,
                            Progress = progress,
                            StudyTimeMinutes = studyTimeMinutes
                        });
                    }
                    else
                    {
                        // 기존 항목이 있으면 업데이트만
                        var existing = existingSubjects.FirstOrDefault(s =>
                            s.SubjectName.Equals(subjectName, StringComparison.OrdinalIgnoreCase));
                        if (existing != null)
                        {
                            existing.Progress = progress;
                            existing.StudyTimeMinutes = studyTimeMinutes;
                            Subjects.Add(existing);
                        }
                    }
                }

                System.Diagnostics.Debug.WriteLine($"[DailyBodyViewModel] 오늘 할 일 과목 {Subjects.Count}개 로드됨 (중복 제거됨)");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DailyBodyViewModel] 오늘 할 일 과목 로드 오류: {ex.Message}");
            }
            finally
            {
                _isLoadingSubjects = false; // 플래그 해제
            }
        }

        private void SaveDailySubjects()
        {
            if (_isLoadingSubjects) return; // 로딩 중이면 저장하지 않음

            try
            {
                // 중복 제거된 과목 리스트만 저장
                var uniqueSubjects = Subjects
                    .GroupBy(s => s.SubjectName.Trim(), StringComparer.OrdinalIgnoreCase)
                    .Select(g => g.First())
                    .ToList();

                // 기존 데이터를 모두 삭제하고 새로 저장 (깔끔한 방법)
                _db.RemoveAllDailySubjects(SelectedDate);

                // 중복 제거된 과목들을 새로 저장
                for (int i = 0; i < uniqueSubjects.Count; i++)
                {
                    var subject = uniqueSubjects[i];
                    _db.SaveDailySubject(SelectedDate, subject.SubjectName, subject.Progress, subject.StudyTimeMinutes, i);
                }

                // UI의 Subjects도 중복 제거된 것으로 업데이트
                if (uniqueSubjects.Count != Subjects.Count)
                {
                    _isLoadingSubjects = true; // 무한 루프 방지
                    Subjects.Clear();
                    foreach (var subject in uniqueSubjects)
                    {
                        Subjects.Add(subject);
                    }
                    _isLoadingSubjects = false;
                }

                System.Diagnostics.Debug.WriteLine($"[DailyBodyViewModel] 오늘 할 일 과목 저장 완료: {uniqueSubjects.Count}개 (중복 제거됨)");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DailyBodyViewModel] 오늘 할 일 과목 저장 오류: {ex.Message}");
            }
        }

        public void AddSubjectSafely(SubjectProgressViewModel subject)
        {
            if (subject == null || string.IsNullOrWhiteSpace(subject.SubjectName))
                return;

            // 중복 확인 - 대소문자 무시하고 정확한 이름 매치
            var existingSubject = Subjects.FirstOrDefault(s =>
                string.Equals(s.SubjectName.Trim(), subject.SubjectName.Trim(), StringComparison.OrdinalIgnoreCase));

            if (existingSubject == null)
            {
                System.Diagnostics.Debug.WriteLine($"[DailyBodyViewModel] 새 과목 추가: {subject.SubjectName}");
                Subjects.Add(subject);
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"[DailyBodyViewModel] 중복 과목 무시: {subject.SubjectName} (이미 존재함)");

                // 기존 과목의 진행률이나 학습시간 업데이트가 필요한 경우
                if (existingSubject.Progress < subject.Progress)
                {
                    existingSubject.Progress = subject.Progress;
                }
                if (existingSubject.StudyTimeMinutes < subject.StudyTimeMinutes)
                {
                    existingSubject.StudyTimeMinutes = subject.StudyTimeMinutes;
                }
            }
        }

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
                }
            }
        }

        public TimeSpan StudyTime { get; set; } = TimeSpan.FromMinutes(165);
        public int Dday => (TargetDate - DateTime.Today).Days;
        public DateTime TargetDate { get; set; } = new DateTime(2025, 6, 22);

        // 실제 DB에서 총 학습시간 계산
        public string TotalStudyTime
        {
            get
            {
                int totalSeconds = _db.GetTotalStudyTimeSeconds();
                TimeSpan totalTime = TimeSpan.FromSeconds(totalSeconds);
                return $"{(int)totalTime.TotalHours}시간 {totalTime.Minutes}분";
            }
        }
    }
}