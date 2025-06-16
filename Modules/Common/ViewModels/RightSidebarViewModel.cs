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
        // 싱글톤 DB 헬퍼 사용
        private readonly DatabaseHelper _db = DatabaseHelper.Instance;
        private DispatcherTimer _timer;
        private TimeSpan _currentSessionTime; // 현재 세션 시간 (내부적으로만 사용)
        private TimeSpan _totalStudyTime;     // 총 학습 시간 (화면에 표시)
        private bool _isRunning;
        private DateTime _sessionStartTime;   // 세션 시작 시간

        // 총 학습 시간을 00:00:00 형식으로 표시 (실시간 업데이트 포함)
        public string TotalStudyTimeDisplay
        {
            get
            {
                // 저장된 총 시간 + 현재 세션 시간을 모두 표시
                var displayTime = _totalStudyTime.Add(_currentSessionTime);
                var result = displayTime.ToString(@"hh\:mm\:ss");
                return result;
            }
        }

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
            _timer.Tick += OnTimerTick;

            // Commands 초기화
            ToggleTimerCommand = new RelayCommand(ToggleTimer);
            DeleteMemoCommand = new RelayCommand<Note>(DeleteMemo);
            AddMemoCommand = new RelayCommand(AddMemo);
            ToggleMemoCommand = new RelayCommand<Note>(ToggleMemo);
            CloseMemoCommand = new RelayCommand<Note>(note =>
            {
                if (note != null)
                    note.IsSelected = false;
            });

            LoadTotalStudyTime();
            LoadMemos();

            // 앱 종료 시 세션 저장을 위한 이벤트 등록
            System.Windows.Application.Current.Exit += (s, e) => EndSession();
        }

        private void OnTimerTick(object sender, EventArgs e)
        {
            _currentSessionTime = _currentSessionTime.Add(TimeSpan.FromSeconds(1));

            // 실시간으로 총 학습시간 업데이트를 위해 TotalStudyTimeDisplay 다시 계산
            OnPropertyChanged(nameof(TotalStudyTimeDisplay));
        }

        private void ToggleTimer()
        {
            if (_isRunning)
            {
                // 타이머 일시정지 - 시간 저장하지만 초기화하지 않음
                _timer.Stop();

                if (_currentSessionTime.TotalSeconds > 0)
                {
                    SaveStudySession();
                    _totalStudyTime = _totalStudyTime.Add(_currentSessionTime);
                    System.Diagnostics.Debug.WriteLine($"[Timer] 일시정지 시 누적 저장: 세션={_currentSessionTime.ToString(@"hh\:mm\:ss")}, 총합={_totalStudyTime.ToString(@"hh\:mm\:ss")}");

                    // 현재 세션 시간은 유지 (초기화하지 않음)
                    // _currentSessionTime = TimeSpan.Zero; // 이 줄을 주석 처리
                }

                OnPropertyChanged(nameof(TotalStudyTimeDisplay));
            }
            else
            {
                // 타이머 재시작 - 기존 시간부터 계속
                _sessionStartTime = DateTime.Now.Subtract(_currentSessionTime); // 시작 시간 조정
                _timer.Start();
                System.Diagnostics.Debug.WriteLine($"[Timer] 타이머 재시작 - 현재 세션: {_currentSessionTime.ToString(@"hh\:mm\:ss")}");
            }

            _isRunning = !_isRunning;
            OnPropertyChanged(nameof(TimerButtonText));
        }

<<<<<<< HEAD
<<<<<<< Updated upstream
=======
=======
>>>>>>> 624f03b473237ab5ecfd5c52cc3b3d95e280b244
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
<<<<<<< HEAD
                //var totalMinutes = _db.GetTotalStudyTimeMinutes(DateTime.Today);
                //_totalStudyTime = TimeSpan.FromMinutes(totalMinutes);
=======
                var totalMinutes = _db.GetTotalStudyTimeMinutes(DateTime.Today);
                _totalStudyTime = TimeSpan.FromMinutes(totalMinutes);
>>>>>>> 624f03b473237ab5ecfd5c52cc3b3d95e280b244
                OnPropertyChanged(nameof(TotalStudyTimeDisplay));

                System.Diagnostics.Debug.WriteLine($"총 학습 시간 로드됨: {_totalStudyTime.ToString(@"hh\:mm\:ss")}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"총 학습 시간 로드 오류: {ex.Message}");
                _totalStudyTime = TimeSpan.Zero;
            }
        }

<<<<<<< HEAD
>>>>>>> Stashed changes
=======
>>>>>>> 624f03b473237ab5ecfd5c52cc3b3d95e280b244
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
            _db.DeleteNote(note.NoteId);
            LoadMemos();
        }
    }
}