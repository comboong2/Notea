using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using System.Windows.Threading;
using SP.ViewModels;
using SP.Modules.Common.Models;
using SP.Modules.Common.Helpers;

namespace SP.Modules.Common.ViewModels
{
    public class RightSidebarViewModel : ViewModelBase
    {
        private readonly DatabaseHelper _db = new();
        private DispatcherTimer _timer;
        private TimeSpan _elapsed;
        private bool _isRunning;

        public string CurrentTime => _elapsed.ToString(@"hh\:mm\:ss");
        public string TimerButtonText => _isRunning ? "일시정지" : "시작";

        public ObservableCollection<Note> Memos { get; } = new();

        public ICommand ToggleTimerCommand { get; }
        public ICommand AddMemoCommand { get; }
        public ICommand ToggleMemoCommand { get; }
        public ICommand CloseMemoCommand { get; }
        public ICommand DeleteMemoCommand { get; }



        private string _newMemoText = string.Empty;
        public string NewMemoText
        {
            get => _newMemoText;
            set => SetProperty(ref _newMemoText, value);
        }

        public RightSidebarViewModel()
        {
            _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _timer.Tick += (s, e) =>
            {
                _elapsed = _elapsed.Add(TimeSpan.FromSeconds(1));
                OnPropertyChanged(nameof(CurrentTime));
            };

            DeleteMemoCommand = new RelayCommand<Note>(DeleteMemo);

            ToggleTimerCommand = new RelayCommand(ToggleTimer);
            AddMemoCommand = new RelayCommand(AddMemo);
            ToggleMemoCommand = new RelayCommand<Note>(ToggleMemo); // 🔹 이 줄 추가
            CloseMemoCommand = new RelayCommand<Note>(note =>
            {
                if (note != null)
                    note.IsSelected = false;
            });

            LoadMemos();
        }

        private void ToggleTimer()
        {
            _isRunning = !_isRunning;
            if (_isRunning)
                _timer.Start();
            else
                _timer.Stop();

            OnPropertyChanged(nameof(TimerButtonText));
        }

<<<<<<< Updated upstream
=======
        // 세션을 완전히 종료하고 저장하는 메소드 (별도로 호출 필요)
        public void EndSession()
        {
            if (_currentSessionTime.TotalMinutes >= 1) // 1분 이상만 저장
            {
                _totalStudyTime = _totalStudyTime.Add(_currentSessionTime);
                SaveStudySession();
                System.Diagnostics.Debug.WriteLine($"세션 종료 및 저장: {_currentSessionTime.ToString(@"hh\:mm\:ss")}");
            }

            // 세션 초기화
            _currentSessionTime = TimeSpan.Zero;
            _timer.Stop();
            _isRunning = false;

            OnPropertyChanged(nameof(TotalStudyTimeDisplay));
            OnPropertyChanged(nameof(TimerButtonText));
        }

        private void SaveStudySession()
        {
            try
            {
                // StudySession 테이블에 세션 정보 저장
                _db.SaveStudySession(_sessionStartTime, DateTime.Now, (int)_currentSessionTime.TotalMinutes);

                System.Diagnostics.Debug.WriteLine($"학습 세션 저장됨: {_currentSessionTime.ToString(@"hh\:mm\:ss")}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"학습 세션 저장 오류: {ex.Message}");
            }
        }

        private void LoadTotalStudyTime()
        {
            try
            {
                // 오늘의 총 학습 시간을 로드
                //var totalMinutes = _db.GetTotalStudyTimeMinutes(DateTime.Today);
                //_totalStudyTime = TimeSpan.FromMinutes(totalMinutes);
                OnPropertyChanged(nameof(TotalStudyTimeDisplay));

                System.Diagnostics.Debug.WriteLine($"총 학습 시간 로드됨: {_totalStudyTime.ToString(@"hh\:mm\:ss")}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"총 학습 시간 로드 오류: {ex.Message}");
                _totalStudyTime = TimeSpan.Zero;
            }
        }

>>>>>>> Stashed changes
        private void AddMemo()
        {
            if (!string.IsNullOrWhiteSpace(NewMemoText))
            {
                var newNote = new Note
                {
                    Content = NewMemoText.Trim()
                };

                _db.SaveNote(newNote);
                NewMemoText = "";
                LoadMemos();
            }
        }

        private void LoadMemos()
        {
            Memos.Clear();
            foreach (var note in _db.GetAllNotes())
                Memos.Add(note);
        }
        private void ToggleMemo(Note note)
        {
            if (note != null)
                note.IsSelected = !note.IsSelected;
        }

        private void DeleteMemo(Note note)
        {
            if (note == null)
            {
                System.Diagnostics.Debug.WriteLine("[삭제 시도] Note가 null입니다.");
                return;
            }
            _db.DeleteNote(note.NoteId); // DB에서 삭제
            LoadMemos();                 // 다시 불러오기
        }

    }
}
