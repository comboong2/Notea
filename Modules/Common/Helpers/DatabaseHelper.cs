using SP.Modules.Common.Models;
using SP.Modules.Daily.Models;
using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using SP.Modules.Subjects.ViewModels;
using System.Collections.ObjectModel;

namespace SP.Modules.Common.Helpers
{
    public class DatabaseHelper
    {
        private static DatabaseHelper _instance;
        private static readonly object _lockObject = new object();
        private readonly string _dbPath;

        // 싱글톤 패턴
        public static DatabaseHelper Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lockObject)
                    {
                        if (_instance == null)
                            _instance = new DatabaseHelper();
                    }
                }
                return _instance;
            }
        }

        private DatabaseHelper()
        {
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            _dbPath = Path.Combine(baseDir, "notea.db");
            Initialize();
        }

        public SQLiteConnection GetConnection()
        {
            // 🆕 DB 락 문제 해결을 위한 연결 설정 개선
            var connectionString = $"Data Source={_dbPath};Version=3;Pooling=true;Max Pool Size=100;Timeout=30;Journal Mode=WAL;";
            return new SQLiteConnection(connectionString);
        }

        public void Initialize()
        {
            lock (_lockObject)
            {
                // 🆕 DB 파일이 다른 프로세스에서 사용 중인지 확인
                try
                {
                    // 기존 연결이 있으면 모두 해제
                    SQLiteConnection.ClearAllPools();
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[DB] 연결 정리 중 오류: {ex.Message}");
                }

                int retryCount = 0;
                int maxRetries = 5;

                while (retryCount < maxRetries)
                {
                    try
                    {
                        using var conn = GetConnection();
                        conn.Open();

                        // WAL 모드 설정 (락 문제 해결)
                        using var pragmaCmd = conn.CreateCommand();
                        pragmaCmd.CommandText = "PRAGMA journal_mode=WAL; PRAGMA synchronous=NORMAL; PRAGMA cache_size=10000; PRAGMA temp_store=memory;";
                        pragmaCmd.ExecuteNonQuery();

                        var cmd = conn.CreateCommand();
                        cmd.CommandText = @"
                            CREATE TABLE IF NOT EXISTS Note (
                                NoteId INTEGER PRIMARY KEY AUTOINCREMENT,
                                Content TEXT NOT NULL,
                                CreatedAt TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
                                UpdatedAt TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP
                            );
                        ";
                        cmd.ExecuteNonQuery();

                        cmd.CommandText = @"
                            CREATE TABLE IF NOT EXISTS Comment (
                                Date TEXT PRIMARY KEY,
                                Text TEXT NOT NULL
                            );
                        ";
                        cmd.ExecuteNonQuery();

                        cmd.CommandText = @"
                            CREATE TABLE IF NOT EXISTS Todo (
                                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                                Date TEXT NOT NULL,
                                Title TEXT NOT NULL,
                                IsCompleted INTEGER NOT NULL DEFAULT 0
                            );
                        ";
                        cmd.ExecuteNonQuery();

                        cmd.CommandText = @"
                            CREATE TABLE IF NOT EXISTS Subject (
                                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                                Name TEXT NOT NULL UNIQUE,
                                TotalStudyTime INTEGER NOT NULL DEFAULT 0
                            );";
                        cmd.ExecuteNonQuery();

                        cmd.CommandText = @"
                            CREATE TABLE IF NOT EXISTS TopicGroup (
                                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                                SubjectId INTEGER NOT NULL,
                                Name TEXT NOT NULL,
                                TotalStudyTime INTEGER NOT NULL DEFAULT 0,
                                FOREIGN KEY (SubjectId) REFERENCES Subject(Id) ON DELETE CASCADE
                            );";
                        cmd.ExecuteNonQuery();

                        cmd.CommandText = @"
                            CREATE TABLE IF NOT EXISTS TopicItem (
                                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                                TopicGroupId INTEGER NOT NULL,
                                Content TEXT NOT NULL,
                                CreatedAt TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
                                FOREIGN KEY (TopicGroupId) REFERENCES TopicGroup(Id) ON DELETE CASCADE
                            );";
                        cmd.ExecuteNonQuery();

                        cmd.CommandText = @"
                            CREATE TABLE IF NOT EXISTS StudySession (
                                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                                StartTime TEXT NOT NULL,
                                EndTime TEXT NOT NULL,
                                DurationSeconds INTEGER NOT NULL,
                                Date TEXT NOT NULL,
                                CreatedAt TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP
                            );";
                        cmd.ExecuteNonQuery();

                        cmd.CommandText = @"
                            CREATE TABLE IF NOT EXISTS DailySubject (
                                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                                Date TEXT NOT NULL,
                                SubjectName TEXT NOT NULL,
                                Progress REAL NOT NULL DEFAULT 0.0,
                                StudyTimeMinutes INTEGER NOT NULL DEFAULT 0,
                                DisplayOrder INTEGER NOT NULL DEFAULT 0
                            );";
                        cmd.ExecuteNonQuery();

                        // 🆕 DailyTopicGroup 테이블 추가
                        cmd.CommandText = @"
                            CREATE TABLE IF NOT EXISTS DailyTopicGroup (
                                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                                Date TEXT NOT NULL,
                                SubjectName TEXT NOT NULL,
                                GroupTitle TEXT NOT NULL,
                                TotalStudyTime INTEGER NOT NULL DEFAULT 0,
                                IsCompleted INTEGER NOT NULL DEFAULT 0
                            );";
                        cmd.ExecuteNonQuery();

                        // 🆕 DailyTopicItem 테이블 추가
                        cmd.CommandText = @"
                            CREATE TABLE IF NOT EXISTS DailyTopicItem (
                                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                                Date TEXT NOT NULL,
                                SubjectName TEXT NOT NULL,
                                GroupTitle TEXT NOT NULL,
                                TopicName TEXT NOT NULL,
                                Progress REAL NOT NULL DEFAULT 0.0,
                                StudyTimeMinutes INTEGER NOT NULL DEFAULT 0,
                                IsCompleted INTEGER NOT NULL DEFAULT 0
                            );";
                        cmd.ExecuteNonQuery();

                        System.Diagnostics.Debug.WriteLine("[DB] 데이터베이스 초기화 완료");
                        break; // 성공하면 루프 종료
                    }
                    catch (SQLiteException ex) when (ex.ErrorCode == 5) // SQLITE_BUSY
                    {
                        retryCount++;
                        System.Diagnostics.Debug.WriteLine($"[DB] 데이터베이스 락, 재시도 {retryCount}/{maxRetries}: {ex.Message}");

                        if (retryCount >= maxRetries)
                        {
                            System.Diagnostics.Debug.WriteLine("[DB] 데이터베이스 락 해결 실패, 프로그램 종료");
                            throw new Exception("데이터베이스에 접근할 수 없습니다. 다른 프로그램에서 사용 중일 수 있습니다.");
                        }

                        System.Threading.Thread.Sleep(1000 * retryCount); // 점진적 지연
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[DB] 초기화 오류: {ex.Message}");
                        throw;
                    }
                }
            }
        }

        // 🆕 안전한 DB 작업을 위한 헬퍼 메소드
        private T ExecuteWithRetry<T>(Func<T> operation, int maxRetries = 3)
        {
            int retryCount = 0;
            while (retryCount < maxRetries)
            {
                try
                {
                    return operation();
                }
                catch (SQLiteException ex) when (ex.ErrorCode == 5) // SQLITE_BUSY
                {
                    retryCount++;
                    if (retryCount >= maxRetries)
                        throw;

                    System.Threading.Thread.Sleep(100 * retryCount);
                    System.Diagnostics.Debug.WriteLine($"[DB] 작업 재시도 {retryCount}/{maxRetries}");
                }
            }
            throw new Exception("DB 작업 재시도 한계 도달");
        }

        private void ExecuteWithRetry(Action operation, int maxRetries = 3)
        {
            ExecuteWithRetry(() => { operation(); return true; }, maxRetries);
        }

        // Note 관련 메소드들 - 안전한 실행으로 래핑
        public List<Note> GetAllNotes()
        {
            return ExecuteWithRetry(() =>
            {
                lock (_lockObject)
                {
                    var list = new List<Note>();
                    using var conn = GetConnection();
                    conn.Open();
                    using var cmd = conn.CreateCommand();
                    cmd.CommandText = "SELECT * FROM Note ORDER BY UpdatedAt DESC";
                    using var reader = cmd.ExecuteReader();
                    while (reader.Read())
                    {
                        list.Add(new Note
                        {
                            NoteId = Convert.ToInt32(reader["NoteId"]),
                            Content = reader["Content"].ToString(),
                            CreatedAt = DateTime.Parse(reader["CreatedAt"].ToString()),
                            UpdatedAt = DateTime.Parse(reader["UpdatedAt"].ToString())
                        });
                    }
                    return list;
                }
            });
        }

        public void SaveNote(Note note)
        {
            ExecuteWithRetry(() =>
            {
                lock (_lockObject)
                {
                    using var conn = GetConnection();
                    conn.Open();
                    using var cmd = conn.CreateCommand();

                    if (note.NoteId == 0)
                    {
                        cmd.CommandText = "INSERT INTO Note (Content) VALUES (@content)";
                    }
                    else
                    {
                        cmd.CommandText = @"
                            UPDATE Note 
                            SET Content = @content, UpdatedAt = CURRENT_TIMESTAMP 
                            WHERE NoteId = @noteId";
                        cmd.Parameters.AddWithValue("@noteId", note.NoteId);
                    }

                    cmd.Parameters.AddWithValue("@content", note.Content);
                    cmd.ExecuteNonQuery();
                }
            });
        }

        public void DeleteNote(int noteId)
        {
            ExecuteWithRetry(() =>
            {
                lock (_lockObject)
                {
                    using var conn = GetConnection();
                    conn.Open();
                    using var cmd = conn.CreateCommand();
                    cmd.CommandText = "DELETE FROM Note WHERE NoteId = @id";
                    cmd.Parameters.AddWithValue("@id", noteId);
                    cmd.ExecuteNonQuery();
                }
            });
        }

        public void UpdateNote(Note note)
        {
            ExecuteWithRetry(() =>
            {
                lock (_lockObject)
                {
                    using var conn = GetConnection();
                    conn.Open();
                    using var cmd = conn.CreateCommand();
                    cmd.CommandText = "UPDATE Note SET Content = @content, UpdatedAt = CURRENT_TIMESTAMP WHERE NoteId = @id";
                    cmd.Parameters.AddWithValue("@content", note.Content);
                    cmd.Parameters.AddWithValue("@id", note.NoteId);
                    cmd.ExecuteNonQuery();
                }
            });
        }

        // Comment 관련 메소드들
        public string GetCommentByDate(DateTime date)
        {
            return ExecuteWithRetry(() =>
            {
                lock (_lockObject)
                {
                    using var conn = GetConnection();
                    conn.Open();
                    using var cmd = conn.CreateCommand();
                    cmd.CommandText = "SELECT Text FROM Comment WHERE Date = @date";
                    cmd.Parameters.AddWithValue("@date", date.ToString("yyyy-MM-dd"));
                    var result = cmd.ExecuteScalar();
                    return result?.ToString() ?? string.Empty;
                }
            });
        }

        public void SaveOrUpdateComment(DateTime date, string text)
        {
            ExecuteWithRetry(() =>
            {
                lock (_lockObject)
                {
                    using var conn = GetConnection();
                    conn.Open();
                    using var cmd = conn.CreateCommand();
                    cmd.CommandText = @"
                        INSERT INTO Comment (Date, Text)
                        VALUES (@date, @text)
                        ON CONFLICT(Date)
                        DO UPDATE SET Text = @text
                    ";
                    cmd.Parameters.AddWithValue("@date", date.ToString("yyyy-MM-dd"));
                    cmd.Parameters.AddWithValue("@text", text);
                    cmd.ExecuteNonQuery();
                }
            });
        }

        // Todo 관련 메소드들
        public List<TodoItem> GetTodosByDate(DateTime date)
        {
            return ExecuteWithRetry(() =>
            {
                lock (_lockObject)
                {
                    var list = new List<TodoItem>();
                    using var conn = GetConnection();
                    conn.Open();
                    using var cmd = conn.CreateCommand();
                    cmd.CommandText = "SELECT * FROM Todo WHERE Date = @date ORDER BY Id";
                    cmd.Parameters.AddWithValue("@date", date.ToString("yyyy-MM-dd"));

                    using var reader = cmd.ExecuteReader();
                    while (reader.Read())
                    {
                        list.Add(new TodoItem
                        {
                            Id = Convert.ToInt32(reader["Id"]),
                            Title = reader["Title"].ToString(),
                            IsCompleted = Convert.ToInt32(reader["IsCompleted"]) == 1
                        });
                    }
                    return list;
                }
            });
        }

        public int AddTodo(DateTime date, string title)
        {
            return ExecuteWithRetry(() =>
            {
                lock (_lockObject)
                {
                    using var conn = GetConnection();
                    conn.Open();
                    using var cmd = conn.CreateCommand();
                    cmd.CommandText = "INSERT INTO Todo (Date, Title, IsCompleted) VALUES (@date, @title, 0); SELECT last_insert_rowid();";
                    cmd.Parameters.AddWithValue("@date", date.ToString("yyyy-MM-dd"));
                    cmd.Parameters.AddWithValue("@title", title);
                    return Convert.ToInt32(cmd.ExecuteScalar());
                }
            });
        }

        public void UpdateTodoCompletion(int id, bool isCompleted)
        {
            ExecuteWithRetry(() =>
            {
                lock (_lockObject)
                {
                    using var conn = GetConnection();
                    conn.Open();
                    using var cmd = conn.CreateCommand();
                    cmd.CommandText = "UPDATE Todo SET IsCompleted = @done WHERE Id = @id";
                    cmd.Parameters.AddWithValue("@done", isCompleted ? 1 : 0);
                    cmd.Parameters.AddWithValue("@id", id);
                    cmd.ExecuteNonQuery();
                }
            });
        }

        public void DeleteTodo(int id)
        {
            ExecuteWithRetry(() =>
            {
                lock (_lockObject)
                {
                    using var conn = GetConnection();
                    conn.Open();
                    using var cmd = conn.CreateCommand();
                    cmd.CommandText = "DELETE FROM Todo WHERE Id = @id";
                    cmd.Parameters.AddWithValue("@id", id);
                    cmd.ExecuteNonQuery();
                }
            });
        }

        // Subject 관련 메소드들
        public int AddSubject(string name)
        {
            return ExecuteWithRetry(() =>
            {
                lock (_lockObject)
                {
                    using var conn = GetConnection();
                    conn.Open();
                    using var cmd = conn.CreateCommand();
                    cmd.CommandText = "INSERT INTO Subject (Name) VALUES (@name); SELECT last_insert_rowid();";
                    cmd.Parameters.AddWithValue("@name", name);
                    return Convert.ToInt32(cmd.ExecuteScalar());
                }
            });
        }

        public int AddTopicGroup(int subjectId, string name)
        {
            return ExecuteWithRetry(() =>
            {
                lock (_lockObject)
                {
                    using var conn = GetConnection();
                    conn.Open();
                    using var cmd = conn.CreateCommand();
                    cmd.CommandText = "INSERT INTO TopicGroup (SubjectId, Name) VALUES (@subjectId, @name); SELECT last_insert_rowid();";
                    cmd.Parameters.AddWithValue("@subjectId", subjectId);
                    cmd.Parameters.AddWithValue("@name", name);
                    return Convert.ToInt32(cmd.ExecuteScalar());
                }
            });
        }

        public int AddTopicItem(int topicGroupId, string content)
        {
            return ExecuteWithRetry(() =>
            {
                lock (_lockObject)
                {
                    using var conn = GetConnection();
                    conn.Open();
                    using var cmd = conn.CreateCommand();
                    cmd.CommandText = "INSERT INTO TopicItem (TopicGroupId, Content) VALUES (@groupId, @content); SELECT last_insert_rowid();";
                    cmd.Parameters.AddWithValue("@groupId", topicGroupId);
                    cmd.Parameters.AddWithValue("@content", content);
                    return Convert.ToInt32(cmd.ExecuteScalar());
                }
            });
        }

        public void UpdateSubjectStudyTime(int subjectId, int seconds)
        {
            ExecuteWithRetry(() =>
            {
                lock (_lockObject)
                {
                    using var conn = GetConnection();
                    conn.Open();
                    using var cmd = conn.CreateCommand();
                    cmd.CommandText = "UPDATE Subject SET TotalStudyTime = TotalStudyTime + @sec WHERE Id = @id";
                    cmd.Parameters.AddWithValue("@sec", seconds);
                    cmd.Parameters.AddWithValue("@id", subjectId);
                    cmd.ExecuteNonQuery();
                }
            });
        }

        public void UpdateTopicGroupStudyTime(int groupId, int seconds)
        {
            ExecuteWithRetry(() =>
            {
                lock (_lockObject)
                {
                    using var conn = GetConnection();
                    conn.Open();
                    using var cmd = conn.CreateCommand();
                    cmd.CommandText = "UPDATE TopicGroup SET TotalStudyTime = TotalStudyTime + @sec WHERE Id = @id";
                    cmd.Parameters.AddWithValue("@sec", seconds);
                    cmd.Parameters.AddWithValue("@id", groupId);
                    cmd.ExecuteNonQuery();
                }
            });
        }

        public List<SubjectGroupViewModel> LoadSubjectsWithGroups()
        {
            return ExecuteWithRetry(() =>
            {
                lock (_lockObject)
                {
                    var result = new List<SubjectGroupViewModel>();

                    using var conn = GetConnection();
                    conn.Open();

                    var cmd = conn.CreateCommand();
                    cmd.CommandText = "SELECT Id, Name, TotalStudyTime FROM Subject ORDER BY Name";
                    using var reader = cmd.ExecuteReader();

                    while (reader.Read())
                    {
                        var subjectId = Convert.ToInt32(reader["Id"]);
                        var subjectName = reader["Name"].ToString();
                        var totalStudyTime = Convert.ToInt32(reader["TotalStudyTime"]);

                        var subjectVM = new SubjectGroupViewModel
                        {
                            SubjectId = subjectId,
                            SubjectName = subjectName,
                            TotalStudyTime = totalStudyTime,
                            TopicGroups = new ObservableCollection<TopicGroupViewModel>()
                        };

                        result.Add(subjectVM);
                    }

                    // 각 과목에 대해 토픽 그룹 로드
                    foreach (var subject in result)
                    {
                        LoadTopicGroupsForSubject(conn, subject);
                    }

                    return result;
                }
            });
        }

        private void LoadTopicGroupsForSubject(SQLiteConnection conn, SubjectGroupViewModel subject)
        {
            using var groupCmd = conn.CreateCommand();
            groupCmd.CommandText = "SELECT Id, Name, TotalStudyTime FROM TopicGroup WHERE SubjectId = @id ORDER BY Name";
            groupCmd.Parameters.AddWithValue("@id", subject.SubjectId);

            using var groupReader = groupCmd.ExecuteReader();
            while (groupReader.Read())
            {
                var groupId = Convert.ToInt32(groupReader["Id"]);
                var groupName = groupReader["Name"].ToString();
                var groupStudyTime = Convert.ToInt32(groupReader["TotalStudyTime"]);

                var topicGroup = new TopicGroupViewModel
                {
                    GroupTitle = groupName,
                    TotalStudyTime = groupStudyTime,
                    ParentSubjectName = subject.SubjectName,
                    Topics = new ObservableCollection<SP.Modules.Subjects.Models.TopicItem>()
                };

                // 과목의 총 학습시간 설정 (비율 계산용)
                topicGroup.SetSubjectTotalTime(subject.TotalStudyTime);

                // 이 TopicGroup에 속한 TopicItem들 로드
                LoadTopicItemsForGroup(conn, topicGroup, groupId);

                subject.TopicGroups.Add(topicGroup);

                System.Diagnostics.Debug.WriteLine($"[DB] TopicGroup '{groupName}' 로드됨, Topics 개수: {topicGroup.Topics.Count}");
            }
        }

        // 새로운 메소드 추가: TopicItem들을 로드
        private void LoadTopicItemsForGroup(SQLiteConnection conn, TopicGroupViewModel topicGroup, int groupId)
        {
            using var itemCmd = conn.CreateCommand();
            itemCmd.CommandText = "SELECT Id, Content, CreatedAt FROM TopicItem WHERE TopicGroupId = @groupId ORDER BY CreatedAt";
            itemCmd.Parameters.AddWithValue("@groupId", groupId);

            using var itemReader = itemCmd.ExecuteReader();
            while (itemReader.Read())
            {
                var itemId = Convert.ToInt32(itemReader["Id"]);
                var content = itemReader["Content"].ToString();
                var createdAt = DateTime.Parse(itemReader["CreatedAt"].ToString());

                var topicItem = new SP.Modules.Subjects.Models.TopicItem
                {
                    Id = itemId,
                    Content = content,
                    ParentTopicGroupName = topicGroup.GroupTitle,
                    ParentSubjectName = topicGroup.ParentSubjectName,
                    Progress = 0.0, // 기본값, 나중에 실제 진행률로 업데이트
                    StudyTimeMinutes = 0 // 기본값
                };

                topicGroup.Topics.Add(topicItem);
            }

            System.Diagnostics.Debug.WriteLine($"[DB] TopicGroup '{topicGroup.GroupTitle}'에 {topicGroup.Topics.Count}개 TopicItem 로드됨");
        }

        // 학습 시간 관련 메소드들 (초 단위 기준)
        public void SaveStudySession(DateTime startTime, DateTime endTime, int durationSeconds)
        {
            ExecuteWithRetry(() =>
            {
                lock (_lockObject)
                {
                    try
                    {
                        using var conn = GetConnection();
                        conn.Open();
                        using var cmd = conn.CreateCommand();
                        cmd.CommandText = @"
                            INSERT INTO StudySession (StartTime, EndTime, DurationSeconds, Date)
                            VALUES (@startTime, @endTime, @duration, @date)";

                        var dateString = startTime.ToString("yyyy-MM-dd");
                        cmd.Parameters.AddWithValue("@startTime", startTime.ToString("yyyy-MM-dd HH:mm:ss"));
                        cmd.Parameters.AddWithValue("@endTime", endTime.ToString("yyyy-MM-dd HH:mm:ss"));
                        cmd.Parameters.AddWithValue("@duration", durationSeconds);
                        cmd.Parameters.AddWithValue("@date", dateString);

                        cmd.ExecuteNonQuery();

                        System.Diagnostics.Debug.WriteLine($"[DB] 학습 세션 저장 성공: {durationSeconds}초, 날짜: {dateString}");
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[DB] 학습 세션 저장 오류: {ex.Message}");
                    }
                }
            });
        }

        // 특정 날짜의 총 학습 시간 가져오기 (초 단위)
        public int GetTotalStudyTimeSeconds(DateTime date)
        {
            return ExecuteWithRetry(() =>
            {
                lock (_lockObject)
                {
                    try
                    {
                        using var conn = GetConnection();
                        conn.Open();
                        using var cmd = conn.CreateCommand();
                        cmd.CommandText = "SELECT COALESCE(SUM(DurationSeconds), 0) FROM StudySession WHERE Date = @date";
                        cmd.Parameters.AddWithValue("@date", date.ToString("yyyy-MM-dd"));

                        var result = cmd.ExecuteScalar();
                        var totalSeconds = Convert.ToInt32(result);

                        System.Diagnostics.Debug.WriteLine($"[DB] 날짜 {date:yyyy-MM-dd}의 총 학습시간: {totalSeconds}초");
                        return totalSeconds;
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[DB] 학습시간 조회 오류: {ex.Message}");
                        return 0;
                    }
                }
            });
        }

        // 전체 학습 시간 가져오기 (초 단위)
        public int GetTotalStudyTimeSeconds()
        {
            return ExecuteWithRetry(() =>
            {
                lock (_lockObject)
                {
                    using var conn = GetConnection();
                    conn.Open();
                    using var cmd = conn.CreateCommand();
                    cmd.CommandText = "SELECT COALESCE(SUM(DurationSeconds), 0) FROM StudySession";

                    var result = cmd.ExecuteScalar();
                    return Convert.ToInt32(result);
                }
            });
        }

        // 호환성을 위한 분 단위 메소드들
        public int GetTotalStudyTimeMinutes(DateTime date)
        {
            var totalSeconds = GetTotalStudyTimeSeconds(date);
            return totalSeconds / 60;
        }

        public int GetTotalStudyTimeMinutes()
        {
            var totalSeconds = GetTotalStudyTimeSeconds();
            return totalSeconds / 60;
        }

        // 과목 학습시간 관련 메소드들
        public int GetTotalAllSubjectsStudyTime()
        {
            return ExecuteWithRetry(() =>
            {
                lock (_lockObject)
                {
                    using var conn = GetConnection();
                    conn.Open();
                    using var cmd = conn.CreateCommand();
                    cmd.CommandText = "SELECT COALESCE(SUM(TotalStudyTime), 0) FROM Subject";
                    var result = cmd.ExecuteScalar();
                    return Convert.ToInt32(result);
                }
            });
        }

        public List<SubjectGroupViewModel> LoadSubjectsWithStudyTime()
        {
            return ExecuteWithRetry(() =>
            {
                lock (_lockObject)
                {
                    var result = new List<SubjectGroupViewModel>();

                    using var conn = GetConnection();
                    conn.Open();
                    using var cmd = conn.CreateCommand();
                    cmd.CommandText = "SELECT Id, Name, TotalStudyTime FROM Subject ORDER BY TotalStudyTime DESC";
                    using var reader = cmd.ExecuteReader();

                    while (reader.Read())
                    {
                        var subjectId = Convert.ToInt32(reader["Id"]);
                        var subjectName = reader["Name"].ToString();
                        var totalStudyTime = Convert.ToInt32(reader["TotalStudyTime"]);

                        var subjectVM = new SubjectGroupViewModel
                        {
                            SubjectId = subjectId,
                            SubjectName = subjectName,
                            TotalStudyTime = totalStudyTime,
                            TopicGroups = new ObservableCollection<TopicGroupViewModel>()
                        };

                        result.Add(subjectVM);
                    }

                    return result;
                }
            });
        }

        // 오늘 할 일 과목 관련 메소드들
        public void SaveDailySubject(DateTime date, string subjectName, double progress, int studyTimeMinutes, int displayOrder)
        {
            ExecuteWithRetry(() =>
            {
                lock (_lockObject)
                {
                    try
                    {
                        using var conn = GetConnection();
                        conn.Open();
                        using var cmd = conn.CreateCommand();
                        cmd.CommandText = @"
                            INSERT OR REPLACE INTO DailySubject (Date, SubjectName, Progress, StudyTimeMinutes, DisplayOrder)
                            VALUES (@date, @subjectName, @progress, @studyTime, @order)";

                        cmd.Parameters.AddWithValue("@date", date.ToString("yyyy-MM-dd"));
                        cmd.Parameters.AddWithValue("@subjectName", subjectName);
                        cmd.Parameters.AddWithValue("@progress", progress);
                        cmd.Parameters.AddWithValue("@studyTime", studyTimeMinutes);
                        cmd.Parameters.AddWithValue("@order", displayOrder);

                        cmd.ExecuteNonQuery();
                        System.Diagnostics.Debug.WriteLine($"[DB] 오늘 할 일 과목 저장: {subjectName}");
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[DB] 오늘 할 일 과목 저장 오류: {ex.Message}");
                    }
                }
            });
        }

        // 🆕 TopicGroups와 함께 DailySubject 저장하는 새로운 메소드
        public void SaveDailySubjectWithTopicGroups(DateTime date, string subjectName, double progress, int studyTimeMinutes, int displayOrder, ObservableCollection<TopicGroupViewModel> topicGroups)
        {
            ExecuteWithRetry(() =>
            {
                lock (_lockObject)
                {
                    try
                    {
                        using var conn = GetConnection();
                        conn.Open();
                        using var transaction = conn.BeginTransaction();

                        try
                        {
                            // 기존 DailySubject 저장
                            using var cmd = conn.CreateCommand();
                            cmd.Transaction = transaction;
                            cmd.CommandText = @"
                                INSERT OR REPLACE INTO DailySubject (Date, SubjectName, Progress, StudyTimeMinutes, DisplayOrder)
                                VALUES (@date, @subjectName, @progress, @studyTime, @order)";

                            cmd.Parameters.AddWithValue("@date", date.ToString("yyyy-MM-dd"));
                            cmd.Parameters.AddWithValue("@subjectName", subjectName);
                            cmd.Parameters.AddWithValue("@progress", progress);
                            cmd.Parameters.AddWithValue("@studyTime", studyTimeMinutes);
                            cmd.Parameters.AddWithValue("@order", displayOrder);
                            cmd.ExecuteNonQuery();

                            // 해당 과목의 기존 DailyTopicGroup 삭제
                            using var deleteGroupCmd = conn.CreateCommand();
                            deleteGroupCmd.Transaction = transaction;
                            deleteGroupCmd.CommandText = "DELETE FROM DailyTopicGroup WHERE Date = @date AND SubjectName = @subjectName";
                            deleteGroupCmd.Parameters.AddWithValue("@date", date.ToString("yyyy-MM-dd"));
                            deleteGroupCmd.Parameters.AddWithValue("@subjectName", subjectName);
                            deleteGroupCmd.ExecuteNonQuery();

                            // 해당 과목의 기존 DailyTopicItem 삭제
                            using var deleteItemCmd = conn.CreateCommand();
                            deleteItemCmd.Transaction = transaction;
                            deleteItemCmd.CommandText = "DELETE FROM DailyTopicItem WHERE Date = @date AND SubjectName = @subjectName";
                            deleteItemCmd.Parameters.AddWithValue("@date", date.ToString("yyyy-MM-dd"));
                            deleteItemCmd.Parameters.AddWithValue("@subjectName", subjectName);
                            deleteItemCmd.ExecuteNonQuery();

                            // TopicGroups 저장
                            foreach (var topicGroup in topicGroups)
                            {
                                using var groupCmd = conn.CreateCommand();
                                groupCmd.Transaction = transaction;
                                groupCmd.CommandText = @"
                                    INSERT INTO DailyTopicGroup (Date, SubjectName, GroupTitle, TotalStudyTime, IsCompleted)
                                    VALUES (@date, @subjectName, @groupTitle, @totalStudyTime, @isCompleted)";

                                groupCmd.Parameters.AddWithValue("@date", date.ToString("yyyy-MM-dd"));
                                groupCmd.Parameters.AddWithValue("@subjectName", subjectName);
                                groupCmd.Parameters.AddWithValue("@groupTitle", topicGroup.GroupTitle);
                                groupCmd.Parameters.AddWithValue("@totalStudyTime", topicGroup.TotalStudyTime);
                                groupCmd.Parameters.AddWithValue("@isCompleted", topicGroup.IsCompleted ? 1 : 0);
                                groupCmd.ExecuteNonQuery();

                                // 각 TopicGroup의 Topics도 저장
                                foreach (var topic in topicGroup.Topics)
                                {
                                    using var topicCmd = conn.CreateCommand();
                                    topicCmd.Transaction = transaction;
                                    topicCmd.CommandText = @"
                                        INSERT INTO DailyTopicItem (Date, SubjectName, GroupTitle, TopicName, Progress, StudyTimeMinutes, IsCompleted)
                                        VALUES (@date, @subjectName, @groupTitle, @topicName, @progress, @studyTime, @isCompleted)";

                                    topicCmd.Parameters.AddWithValue("@date", date.ToString("yyyy-MM-dd"));
                                    topicCmd.Parameters.AddWithValue("@subjectName", subjectName);
                                    topicCmd.Parameters.AddWithValue("@groupTitle", topicGroup.GroupTitle);
                                    topicCmd.Parameters.AddWithValue("@topicName", topic.Name);
                                    topicCmd.Parameters.AddWithValue("@progress", topic.Progress);
                                    topicCmd.Parameters.AddWithValue("@studyTime", topic.StudyTimeMinutes);
                                    topicCmd.Parameters.AddWithValue("@isCompleted", topic.IsCompleted ? 1 : 0);
                                    topicCmd.ExecuteNonQuery();
                                }
                            }

                            transaction.Commit();
                            System.Diagnostics.Debug.WriteLine($"[DB] DailySubject와 TopicGroups 저장 완료: {subjectName} ({topicGroups.Count}개 그룹)");
                        }
                        catch
                        {
                            transaction.Rollback();
                            throw;
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[DB] DailySubject 저장 오류: {ex.Message}");
                    }
                }
            });
        }

        public List<(string SubjectName, double Progress, int StudyTimeMinutes)> GetDailySubjects(DateTime date)
        {
            return ExecuteWithRetry(() =>
            {
                lock (_lockObject)
                {
                    var result = new List<(string, double, int)>();
                    try
                    {
                        using var conn = GetConnection();
                        conn.Open();
                        using var cmd = conn.CreateCommand();
                        cmd.CommandText = "SELECT SubjectName, Progress, StudyTimeMinutes FROM DailySubject WHERE Date = @date ORDER BY DisplayOrder";
                        cmd.Parameters.AddWithValue("@date", date.ToString("yyyy-MM-dd"));

                        using var reader = cmd.ExecuteReader();
                        while (reader.Read())
                        {
                            result.Add((
                                reader["SubjectName"].ToString(),
                                Convert.ToDouble(reader["Progress"]),
                                Convert.ToInt32(reader["StudyTimeMinutes"])
                            ));
                        }

                        System.Diagnostics.Debug.WriteLine($"[DB] 오늘 할 일 과목 {result.Count}개 로드됨");
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[DB] 오늘 할 일 과목 로드 오류: {ex.Message}");
                    }
                    return result;
                }
            });
        }

        // 🆕 TopicGroups와 함께 DailySubject를 로드하는 새로운 메소드
        public List<(string SubjectName, double Progress, int StudyTimeMinutes, List<TopicGroupData> TopicGroups)> GetDailySubjectsWithTopicGroups(DateTime date)
        {
            return ExecuteWithRetry(() =>
            {
                lock (_lockObject)
                {
                    var result = new List<(string, double, int, List<TopicGroupData>)>();
                    try
                    {
                        using var conn = GetConnection();
                        conn.Open();

                        // 과목 정보 조회
                        using var subjectCmd = conn.CreateCommand();
                        subjectCmd.CommandText = "SELECT SubjectName, Progress, StudyTimeMinutes FROM DailySubject WHERE Date = @date ORDER BY DisplayOrder";
                        subjectCmd.Parameters.AddWithValue("@date", date.ToString("yyyy-MM-dd"));

                        var subjects = new List<(string, double, int)>();
                        using (var subjectReader = subjectCmd.ExecuteReader())
                        {
                            while (subjectReader.Read())
                            {
                                subjects.Add((
                                    subjectReader["SubjectName"].ToString(),
                                    Convert.ToDouble(subjectReader["Progress"]),
                                    Convert.ToInt32(subjectReader["StudyTimeMinutes"])
                                ));
                            }
                        }

                        // 각 과목에 대해 TopicGroups 조회
                        foreach (var (subjectName, progress, studyTimeMinutes) in subjects)
                        {
                            var topicGroups = new List<TopicGroupData>();

                            // 해당 과목의 TopicGroups 조회
                            using var groupCmd = conn.CreateCommand();
                            groupCmd.CommandText = "SELECT GroupTitle, TotalStudyTime, IsCompleted FROM DailyTopicGroup WHERE Date = @date AND SubjectName = @subjectName";
                            groupCmd.Parameters.AddWithValue("@date", date.ToString("yyyy-MM-dd"));
                            groupCmd.Parameters.AddWithValue("@subjectName", subjectName);

                            var groups = new List<(string, int, bool)>();
                            using (var groupReader = groupCmd.ExecuteReader())
                            {
                                while (groupReader.Read())
                                {
                                    groups.Add((
                                        groupReader["GroupTitle"].ToString(),
                                        Convert.ToInt32(groupReader["TotalStudyTime"]),
                                        Convert.ToInt32(groupReader["IsCompleted"]) == 1
                                    ));
                                }
                            }

                            // 각 TopicGroup에 대해 Topics 조회
                            foreach (var (groupTitle, totalStudyTime, isCompleted) in groups)
                            {
                                var topics = new List<TopicItemData>();

                                // 해당 TopicGroup의 Topics 조회
                                using var topicCmd = conn.CreateCommand();
                                topicCmd.CommandText = "SELECT TopicName, Progress, StudyTimeMinutes, IsCompleted FROM DailyTopicItem WHERE Date = @date AND SubjectName = @subjectName AND GroupTitle = @groupTitle";
                                topicCmd.Parameters.AddWithValue("@date", date.ToString("yyyy-MM-dd"));
                                topicCmd.Parameters.AddWithValue("@subjectName", subjectName);
                                topicCmd.Parameters.AddWithValue("@groupTitle", groupTitle);

                                using var topicReader = topicCmd.ExecuteReader();
                                while (topicReader.Read())
                                {
                                    topics.Add(new TopicItemData
                                    {
                                        Name = topicReader["TopicName"].ToString(),
                                        Progress = Convert.ToDouble(topicReader["Progress"]),
                                        StudyTimeMinutes = Convert.ToInt32(topicReader["StudyTimeMinutes"]),
                                        IsCompleted = Convert.ToInt32(topicReader["IsCompleted"]) == 1
                                    });
                                }

                                topicGroups.Add(new TopicGroupData
                                {
                                    GroupTitle = groupTitle,
                                    TotalStudyTime = totalStudyTime,
                                    IsCompleted = isCompleted,
                                    Topics = topics
                                });
                            }

                            result.Add((subjectName, progress, studyTimeMinutes, topicGroups));
                        }

                        System.Diagnostics.Debug.WriteLine($"[DB] {result.Count}개 DailySubject (TopicGroups 포함) 로드됨");
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[DB] DailySubject 로드 오류: {ex.Message}");
                    }
                    return result;
                }
            });
        }

        public void RemoveDailySubject(DateTime date, string subjectName)
        {
            ExecuteWithRetry(() =>
            {
                lock (_lockObject)
                {
                    try
                    {
                        using var conn = GetConnection();
                        conn.Open();
                        using var transaction = conn.BeginTransaction();

                        try
                        {
                            // DailySubject 삭제
                            using var cmd = conn.CreateCommand();
                            cmd.Transaction = transaction;
                            cmd.CommandText = "DELETE FROM DailySubject WHERE Date = @date AND SubjectName = @subjectName";
                            cmd.Parameters.AddWithValue("@date", date.ToString("yyyy-MM-dd"));
                            cmd.Parameters.AddWithValue("@subjectName", subjectName);
                            cmd.ExecuteNonQuery();

                            // 관련 DailyTopicGroup 삭제
                            using var groupCmd = conn.CreateCommand();
                            groupCmd.Transaction = transaction;
                            groupCmd.CommandText = "DELETE FROM DailyTopicGroup WHERE Date = @date AND SubjectName = @subjectName";
                            groupCmd.Parameters.AddWithValue("@date", date.ToString("yyyy-MM-dd"));
                            groupCmd.Parameters.AddWithValue("@subjectName", subjectName);
                            groupCmd.ExecuteNonQuery();

                            // 관련 DailyTopicItem 삭제
                            using var itemCmd = conn.CreateCommand();
                            itemCmd.Transaction = transaction;
                            itemCmd.CommandText = "DELETE FROM DailyTopicItem WHERE Date = @date AND SubjectName = @subjectName";
                            itemCmd.Parameters.AddWithValue("@date", date.ToString("yyyy-MM-dd"));
                            itemCmd.Parameters.AddWithValue("@subjectName", subjectName);
                            itemCmd.ExecuteNonQuery();

                            transaction.Commit();
                            System.Diagnostics.Debug.WriteLine($"[DB] 오늘 할 일 과목과 관련 데이터 삭제: {subjectName}");
                        }
                        catch
                        {
                            transaction.Rollback();
                            throw;
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[DB] 오늘 할 일 과목 삭제 오류: {ex.Message}");
                    }
                }
            });
        }

        // 특정 날짜의 모든 DailySubject 삭제
        public void RemoveAllDailySubjects(DateTime date)
        {
            ExecuteWithRetry(() =>
            {
                lock (_lockObject)
                {
                    try
                    {
                        using var conn = GetConnection();
                        conn.Open();
                        using var transaction = conn.BeginTransaction();

                        try
                        {
                            // DailySubject 삭제
                            using var cmd = conn.CreateCommand();
                            cmd.Transaction = transaction;
                            cmd.CommandText = "DELETE FROM DailySubject WHERE Date = @date";
                            cmd.Parameters.AddWithValue("@date", date.ToString("yyyy-MM-dd"));
                            cmd.ExecuteNonQuery();

                            // DailyTopicGroup 삭제
                            using var groupCmd = conn.CreateCommand();
                            groupCmd.Transaction = transaction;
                            groupCmd.CommandText = "DELETE FROM DailyTopicGroup WHERE Date = @date";
                            groupCmd.Parameters.AddWithValue("@date", date.ToString("yyyy-MM-dd"));
                            groupCmd.ExecuteNonQuery();

                            // DailyTopicItem 삭제
                            using var itemCmd = conn.CreateCommand();
                            itemCmd.Transaction = transaction;
                            itemCmd.CommandText = "DELETE FROM DailyTopicItem WHERE Date = @date";
                            itemCmd.Parameters.AddWithValue("@date", date.ToString("yyyy-MM-dd"));
                            itemCmd.ExecuteNonQuery();

                            transaction.Commit();
                            System.Diagnostics.Debug.WriteLine($"[DB] 해당 날짜의 모든 오늘 할 일 과목과 관련 데이터 삭제: {date:yyyy-MM-dd}");
                        }
                        catch
                        {
                            transaction.Rollback();
                            throw;
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[DB] 모든 오늘 할 일 과목 삭제 오류: {ex.Message}");
                    }
                }
            });
        }

        // 🆕 체크박스 상태 업데이트 메소드 추가
        public void UpdateDailyTopicGroupCompletion(DateTime date, string subjectName, string groupTitle, bool isCompleted)
        {
            ExecuteWithRetry(() =>
            {
                lock (_lockObject)
                {
                    try
                    {
                        using var conn = GetConnection();
                        conn.Open();
                        using var cmd = conn.CreateCommand();
                        cmd.CommandText = "UPDATE DailyTopicGroup SET IsCompleted = @isCompleted WHERE Date = @date AND SubjectName = @subjectName AND GroupTitle = @groupTitle";
                        cmd.Parameters.AddWithValue("@isCompleted", isCompleted ? 1 : 0);
                        cmd.Parameters.AddWithValue("@date", date.ToString("yyyy-MM-dd"));
                        cmd.Parameters.AddWithValue("@subjectName", subjectName);
                        cmd.Parameters.AddWithValue("@groupTitle", groupTitle);
                        cmd.ExecuteNonQuery();

                        System.Diagnostics.Debug.WriteLine($"[DB] TopicGroup 체크 상태 업데이트: {groupTitle} = {isCompleted}");
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[DB] TopicGroup 체크 상태 업데이트 오류: {ex.Message}");
                    }
                }
            });
        }

        public void UpdateDailyTopicItemCompletion(DateTime date, string subjectName, string groupTitle, string topicName, bool isCompleted)
        {
            ExecuteWithRetry(() =>
            {
                lock (_lockObject)
                {
                    try
                    {
                        using var conn = GetConnection();
                        conn.Open();
                        using var cmd = conn.CreateCommand();
                        cmd.CommandText = "UPDATE DailyTopicItem SET IsCompleted = @isCompleted WHERE Date = @date AND SubjectName = @subjectName AND GroupTitle = @groupTitle AND TopicName = @topicName";
                        cmd.Parameters.AddWithValue("@isCompleted", isCompleted ? 1 : 0);
                        cmd.Parameters.AddWithValue("@date", date.ToString("yyyy-MM-dd"));
                        cmd.Parameters.AddWithValue("@subjectName", subjectName);
                        cmd.Parameters.AddWithValue("@groupTitle", groupTitle);
                        cmd.Parameters.AddWithValue("@topicName", topicName);
                        cmd.ExecuteNonQuery();

                        System.Diagnostics.Debug.WriteLine($"[DB] TopicItem 체크 상태 업데이트: {topicName} = {isCompleted}");
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[DB] TopicItem 체크 상태 업데이트 오류: {ex.Message}");
                    }
                }
            });
        }

        // IDisposable 구현 (메모리 누수 방지)
        public void Dispose()
        {
            try
            {
                SQLiteConnection.ClearAllPools();
                GC.Collect();
                GC.WaitForPendingFinalizers();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DB] Dispose 오류: {ex.Message}");
            }
        }
    }

    // 🆕 데이터 전송용 클래스들 추가
    public class TopicGroupData
    {
        public string GroupTitle { get; set; } = string.Empty;
        public int TotalStudyTime { get; set; }
        public bool IsCompleted { get; set; }
        public List<TopicItemData> Topics { get; set; } = new();
    }

    public class TopicItemData
    {
        public string Name { get; set; } = string.Empty;
        public double Progress { get; set; }
        public int StudyTimeMinutes { get; set; }
        public bool IsCompleted { get; set; }
    }
}