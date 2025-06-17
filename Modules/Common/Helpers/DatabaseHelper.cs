using SP.Modules.Common.Models;
using SP.Modules.Daily.Models;
using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using SP.Modules.Subjects.ViewModels;
using System.Collections.ObjectModel;
using System.Data; // Added for DataTable

namespace SP.Modules.Common.Helpers
{
    public class DatabaseHelper : IDisposable // IDisposable을 구현하여 리소스 관리
    {
        private static DatabaseHelper _instance;
        private static readonly object _lockObject = new object();
        private readonly string _dbPath;
        private readonly string _connectionString; // 연결 문자열 저장

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
            _dbPath = Path.Combine(baseDir, "notea.db"); // DB 파일 경로 설정
            _connectionString = $"Data Source={_dbPath};Version=3;Pooling=true;Max Pool Size=100;Timeout=30;Journal Mode=WAL;";

            // 데이터베이스 초기화
            Initialize();
        }

        public SQLiteConnection GetConnection()
        {
            return new SQLiteConnection(_connectionString);
        }

        public void Initialize()
        {
            lock (_lockObject)
            {
                try
                {
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

                        using var pragmaCmd = conn.CreateCommand();
                        // 외래 키 활성화 및 저널 모드 설정
                        pragmaCmd.CommandText = "PRAGMA foreign_keys = ON; PRAGMA journal_mode=WAL; PRAGMA synchronous=NORMAL; PRAGMA cache_size=10000; PRAGMA temp_store=memory;";
                        pragmaCmd.ExecuteNonQuery();

                        var cmd = conn.CreateCommand();

                        // SP 프로젝트의 기존 테이블들
                        cmd.CommandText = @"
                            CREATE TABLE IF NOT EXISTS Note (
                                NoteId INTEGER PRIMARY KEY AUTOINCREMENT,
                                Content TEXT NOT NULL,
                                CreatedAt TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
                                UpdatedAt TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP
                            );";
                        cmd.ExecuteNonQuery();

                        cmd.CommandText = @"
                            CREATE TABLE IF NOT EXISTS Comment (
                                Date TEXT PRIMARY KEY,
                                Text TEXT NOT NULL
                            );";
                        cmd.ExecuteNonQuery();

                        cmd.CommandText = @"
                            CREATE TABLE IF NOT EXISTS Todo (
                                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                                Date TEXT NOT NULL,
                                Title TEXT NOT NULL,
                                IsCompleted INTEGER NOT NULL DEFAULT 0
                            );";
                        cmd.ExecuteNonQuery();

                        cmd.CommandText = @"
                            CREATE TABLE IF NOT EXISTS Subject (
                                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                                Name TEXT NOT NULL UNIQUE,
                                TotalStudyTimeSeconds INTEGER NOT NULL DEFAULT 0
                            );";
                        cmd.ExecuteNonQuery();

                        cmd.CommandText = @"
                            CREATE TABLE IF NOT EXISTS TopicGroup (
                                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                                SubjectId INTEGER NOT NULL,
                                Name TEXT NOT NULL,
                                TotalStudyTimeSeconds INTEGER NOT NULL DEFAULT 0,
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
                                CreatedAt TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
                                SubjectName TEXT NULL, -- 학습 세션 추적을 위한 컬럼 추가
                                TopicGroupName TEXT NULL -- 학습 세션 추적을 위한 컬럼 추가
                            );";
                        cmd.ExecuteNonQuery();

                        cmd.CommandText = @"
                            CREATE TABLE IF NOT EXISTS DailySubject (
                                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                                Date TEXT NOT NULL,
                                SubjectName TEXT NOT NULL,
                                Progress REAL NOT NULL DEFAULT 0.0,
                                StudyTimeSeconds INTEGER NOT NULL DEFAULT 0,
                                DisplayOrder INTEGER NOT NULL DEFAULT 0
                            );";
                        cmd.ExecuteNonQuery();

                        cmd.CommandText = @"
                            CREATE TABLE IF NOT EXISTS DailyTopicGroup (
                                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                                Date TEXT NOT NULL,
                                SubjectName TEXT NOT NULL,
                                GroupTitle TEXT NOT NULL,
                                TotalStudyTimeSeconds INTEGER NOT NULL DEFAULT 0,
                                IsCompleted INTEGER NOT NULL DEFAULT 0
                            );";
                        cmd.ExecuteNonQuery();

                        cmd.CommandText = @"
                            CREATE TABLE IF NOT EXISTS DailyTopicItem (
                                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                                Date TEXT NOT NULL,
                                SubjectName TEXT NOT NULL,
                                GroupTitle TEXT NOT NULL,
                                TopicName TEXT NOT NULL,
                                Progress REAL NOT NULL DEFAULT 0.0,
                                StudyTimeSeconds INTEGER NOT NULL DEFAULT 0,
                                IsCompleted INTEGER NOT NULL DEFAULT 0
                            );";
                        cmd.ExecuteNonQuery();

                        // Notea 프로젝트에서 가져온 추가 테이블들
                        cmd.CommandText = @"
                            CREATE TABLE IF NOT EXISTS category (
                                categoryId INTEGER PRIMARY KEY AUTOINCREMENT,
                                displayOrder INTEGER DEFAULT 0,
                                title VARCHAR NOT NULL,
                                subJectId INTEGER NOT NULL,
                                timeId INTEGER NOT NULL,
                                level INTEGER DEFAULT 1,
                                parentCategoryId INTEGER DEFAULT NULL,
                                FOREIGN KEY (subJectId) REFERENCES subject (Id), 
                                FOREIGN KEY (timeId) REFERENCES time (timeId)
                            );";
                        cmd.ExecuteNonQuery();

                        cmd.CommandText = @"
                            CREATE TABLE IF NOT EXISTS monthlyEvent (
                                planId INTEGER PRIMARY KEY AUTOINCREMENT,
                                title VARCHAR NOT NULL,
                                description VARCHAR NULL,
                                isDday BOOLEAN NOT NULL,
                                startDate DATETIME NOT NULL,
                                endDate DATETIME NOT NULL,
                                color VARCHAR NULL
                            );";
                        cmd.ExecuteNonQuery();

                        cmd.CommandText = @"
                            CREATE TABLE IF NOT EXISTS noteContent (
                                textId INTEGER PRIMARY KEY AUTOINCREMENT,
                                displayOrder INTEGER DEFAULT 0,
                                content VARCHAR NULL,
                                categoryId INTEGER NOT NULL,
                                subJectId INTEGER NOT NULL,
                                imageUrl VARCHAR DEFAULT NULL,
                                contentType VARCHAR DEFAULT 'text',
                                FOREIGN KEY (categoryId) REFERENCES category (categoryId),
                                FOREIGN KEY (subJectId) REFERENCES Subject (Id) 
                            );";
                        cmd.ExecuteNonQuery();

                        cmd.CommandText = @"
                            CREATE TABLE IF NOT EXISTS time (
                                timeId INTEGER PRIMARY KEY AUTOINCREMENT,
                                createDate DATETIME NOT NULL,
                                record INT NOT NULL
                            );";
                        cmd.ExecuteNonQuery();

                        // 초기 데이터 삽입 (Notea 프로젝트에서 가져옴)
                        cmd.CommandText = "INSERT OR IGNORE INTO Subject (Id, Name) VALUES (1, '윈도우즈 프로그래밍');";
                        cmd.ExecuteNonQuery();

                        // 스키마 업데이트 (Notea 프로젝트에서 가져옴)
                        UpdateSchemaForHeadingLevel(conn);
                        UpdateSchemaForImageSupport(conn);
                        UpdateSchemaForMonthlyComment(conn); // 추가된 메서드 호출

                        System.Diagnostics.Debug.WriteLine("[DB] 데이터베이스 초기화 완료");
                        break;
                    }
                    catch (SQLiteException ex) when (ex.ErrorCode == 5)
                    {
                        retryCount++;
                        System.Diagnostics.Debug.WriteLine($"[DB] 데이터베이스 락, 재시도 {retryCount}/{maxRetries}: {ex.Message}");

                        if (retryCount >= maxRetries)
                        {
                            System.Diagnostics.Debug.WriteLine("[DB] 데이터베이스 락 해결 실패, 프로그램 종료");
                            throw new Exception("데이터베이스에 접근할 수 없습니다. 다른 프로그램에서 사용 중일 수 있습니다.");
                        }

                        System.Threading.Thread.Sleep(1000 * retryCount);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[DB] 초기화 오류: {ex.Message}");
                        throw;
                    }
                }
            }
        }

        private T ExecuteWithRetry<T>(Func<T> operation, int maxRetries = 3)
        {
            int retryCount = 0;
            while (retryCount < maxRetries)
            {
                try
                {
                    return operation();
                }
                catch (SQLiteException ex) when (ex.ErrorCode == 5)
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

        // ===== Note 관련 메소드들 =====
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

        // ===== Comment 관련 메소드들 =====
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
                        DO UPDATE SET Text = @text";
                    cmd.Parameters.AddWithValue("@date", date.ToString("yyyy-MM-dd"));
                    cmd.Parameters.AddWithValue("@text", text);
                    cmd.ExecuteNonQuery();
                }
            });
        }

        // ===== Todo 관련 메소드들 =====
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

        // ===== Subject 관련 메소드들 (초단위) =====
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

        public void UpdateSubjectStudyTimeSeconds(int subjectId, int seconds)
        {
            ExecuteWithRetry(() =>
            {
                lock (_lockObject)
                {
                    using var conn = GetConnection();
                    conn.Open();
                    using var cmd = conn.CreateCommand();
                    cmd.CommandText = "UPDATE Subject SET TotalStudyTimeSeconds = TotalStudyTimeSeconds + @sec WHERE Id = @id";
                    cmd.Parameters.AddWithValue("@sec", seconds);
                    cmd.Parameters.AddWithValue("@id", subjectId);
                    cmd.ExecuteNonQuery();
                }
            });
        }

        public void UpdateTopicGroupStudyTimeSeconds(int groupId, int seconds)
        {
            ExecuteWithRetry(() =>
            {
                lock (_lockObject)
                {
                    using var conn = GetConnection();
                    conn.Open();
                    using var cmd = conn.CreateCommand();
                    cmd.CommandText = "UPDATE TopicGroup SET TotalStudyTimeSeconds = TotalStudyTimeSeconds + @sec WHERE Id = @id";
                    cmd.Parameters.AddWithValue("@sec", seconds);
                    cmd.Parameters.AddWithValue("@id", groupId);
                    cmd.ExecuteNonQuery();
                }
            });
        }

        // LoadSubjectsWithGroups 메소드
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
                    cmd.CommandText = "SELECT Id, Name, TotalStudyTimeSeconds FROM Subject ORDER BY Name";
                    using var reader = cmd.ExecuteReader();

                    while (reader.Read())
                    {
                        var subjectId = Convert.ToInt32(reader["Id"]);
                        var subjectName = reader["Name"].ToString();
                        var totalStudyTimeSeconds = Convert.ToInt32(reader["TotalStudyTimeSeconds"]);

                        var subjectVM = new SubjectGroupViewModel
                        {
                            SubjectId = subjectId,
                            SubjectName = subjectName,
                            TotalStudyTimeSeconds = totalStudyTimeSeconds,
                            TopicGroups = new ObservableCollection<TopicGroupViewModel>()
                        };

                        result.Add(subjectVM);
                    }

                    foreach (var subject in result)
                    {
                        LoadTopicGroupsForSubject(conn, subject);
                    }

                    return result;
                }
            });
        }

        // LoadTopicGroupsForSubject 메소드
        private void LoadTopicGroupsForSubject(SQLiteConnection conn, SubjectGroupViewModel subject)
        {
            using var groupCmd = conn.CreateCommand();
            groupCmd.CommandText = "SELECT Id, Name, TotalStudyTimeSeconds FROM TopicGroup WHERE SubjectId = @id ORDER BY Name";
            groupCmd.Parameters.AddWithValue("@id", subject.SubjectId);

            using var groupReader = groupCmd.ExecuteReader();
            while (groupReader.Read())
            {
                var groupId = Convert.ToInt32(groupReader["Id"]);
                var groupName = groupReader["Name"].ToString();
                var groupStudyTimeSeconds = Convert.ToInt32(groupReader["TotalStudyTimeSeconds"]);

                var topicGroup = new TopicGroupViewModel
                {
                    GroupTitle = groupName,
                    TotalStudyTimeSeconds = groupStudyTimeSeconds,
                    ParentSubjectName = subject.SubjectName,
                    Topics = new ObservableCollection<SP.Modules.Subjects.Models.TopicItem>()
                };

                topicGroup.SetSubjectTotalTime(subject.TotalStudyTimeSeconds);

                LoadTopicItemsForGroup(conn, topicGroup, groupId);
                subject.TopicGroups.Add(topicGroup);

                System.Diagnostics.Debug.WriteLine($"[DB] TopicGroup '{groupName}' 로드됨, Topics 개수: {topicGroup.Topics.Count}");
            }
        }

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
                    Progress = 0.0,
                    StudyTimeSeconds = 0
                };

                topicGroup.Topics.Add(topicItem);
            }

            System.Diagnostics.Debug.WriteLine($"[DB] TopicGroup '{topicGroup.GroupTitle}'에 {topicGroup.Topics.Count}개 TopicItem 로드됨");
        }


        // SaveStudySession (5 arguments)
        public void SaveStudySession(DateTime startTime, DateTime endTime, int durationSeconds,
            string subjectName = null, string topicGroupName = null)
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
                    INSERT INTO StudySession (StartTime, EndTime, DurationSeconds, Date, SubjectName, TopicGroupName)
                    VALUES (@startTime, @endTime, @duration, @date, @subjectName, @topicGroupName)";

                        var dateString = startTime.ToString("yyyy-MM-dd");
                        cmd.Parameters.AddWithValue("@startTime", startTime.ToString("yyyy-MM-dd HH:mm:ss"));
                        cmd.Parameters.AddWithValue("@endTime", endTime.ToString("yyyy-MM-dd HH:mm:ss"));
                        cmd.Parameters.AddWithValue("@duration", durationSeconds);
                        cmd.Parameters.AddWithValue("@date", dateString);
                        cmd.Parameters.AddWithValue("@subjectName", subjectName ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@topicGroupName", topicGroupName ?? (object)DBNull.Value);

                        cmd.ExecuteNonQuery();

                        var logMsg = $"[DB] 학습 세션 저장: {durationSeconds}초";
                        if (!string.IsNullOrEmpty(subjectName))
                            logMsg += $", 과목: {subjectName}";
                        if (!string.IsNullOrEmpty(topicGroupName))
                            logMsg += $", 분류: {topicGroupName}";

                        System.Diagnostics.Debug.WriteLine(logMsg);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[DB] 학습 세션 저장 오류: {ex.Message}");
                    }
                }
            });
        }

        // SaveStudySession (3 arguments for compatibility)
        public void SaveStudySession(DateTime startTime, DateTime endTime, int durationSeconds)
        {
            SaveStudySession(startTime, endTime, durationSeconds, null, null);
        }

        // GetTotalStudyTimeSeconds (with date)
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

        public int GetTotalAllSubjectsStudyTimeSeconds()
        {
            return ExecuteWithRetry(() =>
            {
                lock (_lockObject)
                {
                    using var conn = GetConnection();
                    conn.Open();
                    using var cmd = conn.CreateCommand();
                    cmd.CommandText = "SELECT COALESCE(SUM(TotalStudyTimeSeconds), 0) FROM Subject";
                    var result = cmd.ExecuteScalar();
                    return Convert.ToInt32(result);
                }
            });
        }

        public int GetSubjectTotalStudyTimeSeconds(string subjectName)
        {
            return ExecuteWithRetry(() =>
            {
                lock (_lockObject)
                {
                    using var conn = GetConnection();
                    conn.Open();
                    using var cmd = conn.CreateCommand();
                    cmd.CommandText = "SELECT COALESCE(TotalStudyTimeSeconds, 0) FROM Subject WHERE Name = @name";
                    cmd.Parameters.AddWithValue("@name", subjectName);

                    var result = cmd.ExecuteScalar();
                    int totalTimeSeconds = Convert.ToInt32(result);

                    System.Diagnostics.Debug.WriteLine($"[DB] 과목 '{subjectName}' 총 학습시간: {totalTimeSeconds}초");
                    return totalTimeSeconds;
                }
            });
        }

        // ===== Daily Subject 관련 메소드들 (초단위) =====
        public void SaveDailySubject(DateTime date, string subjectName, double progress, int studyTimeSeconds, int displayOrder)
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
                            INSERT OR REPLACE INTO DailySubject (Date, SubjectName, Progress, StudyTimeSeconds, DisplayOrder)
                            VALUES (@date, @subjectName, @progress, @studyTime, @order)";

                        cmd.Parameters.AddWithValue("@date", date.ToString("yyyy-MM-dd"));
                        cmd.Parameters.AddWithValue("@subjectName", subjectName);
                        cmd.Parameters.AddWithValue("@progress", progress);
                        cmd.Parameters.AddWithValue("@studyTime", studyTimeSeconds);
                        cmd.Parameters.AddWithValue("@order", displayOrder);

                        cmd.ExecuteNonQuery();
                        System.Diagnostics.Debug.WriteLine($"[DB] 오늘 할 일 과목 저장: {subjectName} ({studyTimeSeconds}초)");
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[DB] 오늘 할 일 과목 저장 오류: {ex.Message}");
                    }
                }
            });
        }

        public void SaveDailySubjectWithTopicGroups(DateTime date, string subjectName, double progress, int studyTimeSeconds, int displayOrder, ObservableCollection<TopicGroupViewModel> topicGroups)
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
                            // DailySubject 저장
                            using var cmd = conn.CreateCommand();
                            cmd.Transaction = transaction;
                            cmd.CommandText = @"
                                INSERT OR REPLACE INTO DailySubject (Date, SubjectName, Progress, StudyTimeSeconds, DisplayOrder)
                                VALUES (@date, @subjectName, @progress, @studyTime, @order)";

                            cmd.Parameters.AddWithValue("@date", date.ToString("yyyy-MM-dd"));
                            cmd.Parameters.AddWithValue("@subjectName", subjectName);
                            cmd.Parameters.AddWithValue("@progress", progress);
                            cmd.Parameters.AddWithValue("@studyTime", studyTimeSeconds);
                            cmd.Parameters.AddWithValue("@order", displayOrder);
                            cmd.ExecuteNonQuery();

                            // 기존 TopicGroup 삭제
                            using var deleteGroupCmd = conn.CreateCommand();
                            deleteGroupCmd.Transaction = transaction;
                            deleteGroupCmd.CommandText = "DELETE FROM DailyTopicGroup WHERE Date = @date AND SubjectName = @subjectName";
                            deleteGroupCmd.Parameters.AddWithValue("@date", date.ToString("yyyy-MM-dd"));
                            deleteGroupCmd.Parameters.AddWithValue("@subjectName", subjectName);
                            deleteGroupCmd.ExecuteNonQuery();

                            // 기존 TopicItem 삭제
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
                                    INSERT INTO DailyTopicGroup (Date, SubjectName, GroupTitle, TotalStudyTimeSeconds, IsCompleted)
                                    VALUES (@date, @subjectName, @groupTitle, @totalStudyTime, @isCompleted)";

                                groupCmd.Parameters.AddWithValue("@date", date.ToString("yyyy-MM-dd"));
                                groupCmd.Parameters.AddWithValue("@subjectName", subjectName);
                                groupCmd.Parameters.AddWithValue("@groupTitle", topicGroup.GroupTitle);
                                groupCmd.Parameters.AddWithValue("@totalStudyTime", topicGroup.TotalStudyTimeSeconds);
                                groupCmd.Parameters.AddWithValue("@isCompleted", topicGroup.IsCompleted ? 1 : 0);
                                groupCmd.ExecuteNonQuery();

                                // TopicItems 저장
                                foreach (var topic in topicGroup.Topics)
                                {
                                    using var topicCmd = conn.CreateCommand();
                                    topicCmd.Transaction = transaction;
                                    topicCmd.CommandText = @"
                                        INSERT INTO DailyTopicItem (Date, SubjectName, GroupTitle, TopicName, Progress, StudyTimeSeconds, IsCompleted)
                                        VALUES (@date, @subjectName, @groupTitle, @topicName, @progress, @studyTime, @isCompleted)";

                                    topicCmd.Parameters.AddWithValue("@date", date.ToString("yyyy-MM-dd"));
                                    cmd.Parameters.AddWithValue("@subjectName", subjectName);
                                    topicCmd.Parameters.AddWithValue("@groupTitle", topicGroup.GroupTitle);
                                    topicCmd.Parameters.AddWithValue("@topicName", topic.Name);
                                    topicCmd.Parameters.AddWithValue("@progress", topic.Progress);
                                    topicCmd.Parameters.AddWithValue("@studyTime", topic.StudyTimeSeconds);
                                    topicCmd.Parameters.AddWithValue("@isCompleted", topic.IsCompleted ? 1 : 0);
                                    topicCmd.ExecuteNonQuery();
                                }
                            }

                            transaction.Commit();
                            System.Diagnostics.Debug.WriteLine($"[DB] DailySubject와 TopicGroups 저장 완료: {subjectName} ({topicGroups.Count}개 그룹, {studyTimeSeconds}초)");
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

        public List<(string SubjectName, double Progress, int StudyTimeSeconds, List<TopicGroupData> TopicGroups)> GetDailySubjectsWithTopicGroups(DateTime date)
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
                        subjectCmd.CommandText = "SELECT SubjectName, Progress, StudyTimeSeconds FROM DailySubject WHERE Date = @date ORDER BY DisplayOrder";
                        subjectCmd.Parameters.AddWithValue("@date", date.ToString("yyyy-MM-dd"));

                        var subjects = new List<(string, double, int)>();
                        using (var subjectReader = subjectCmd.ExecuteReader())
                        {
                            while (subjectReader.Read())
                            {
                                subjects.Add((
                                    subjectReader["SubjectName"].ToString(),
                                    Convert.ToDouble(subjectReader["Progress"]),
                                    Convert.ToInt32(subjectReader["StudyTimeSeconds"])
                                ));
                            }
                        }

                        // 각 과목에 대해 TopicGroups 조회
                        foreach (var (subjectName, progress, studyTimeSeconds) in subjects)
                        {
                            var topicGroups = new List<TopicGroupData>();

                            // TopicGroups 조회
                            using var groupCmd = conn.CreateCommand();
                            groupCmd.CommandText = "SELECT GroupTitle, TotalStudyTimeSeconds, IsCompleted FROM DailyTopicGroup WHERE Date = @date AND SubjectName = @subjectName";
                            groupCmd.Parameters.AddWithValue("@date", date.ToString("yyyy-MM-dd"));
                            groupCmd.Parameters.AddWithValue("@subjectName", subjectName);

                            var groups = new List<(string, int, bool)>();
                            using (var groupReader = groupCmd.ExecuteReader())
                            {
                                while (groupReader.Read())
                                {
                                    groups.Add((
                                        groupReader["GroupTitle"].ToString(),
                                        Convert.ToInt32(groupReader["TotalStudyTimeSeconds"]),
                                        Convert.ToInt32(groupReader["IsCompleted"]) == 1
                                    ));
                                }
                            }

                            // 각 TopicGroup의 Topics 조회
                            foreach (var (groupTitle, totalStudyTimeSeconds, isCompleted) in groups)
                            {
                                var topics = new List<TopicItemData>();

                                using var topicCmd = conn.CreateCommand();
                                topicCmd.CommandText = "SELECT TopicName, Progress, StudyTimeSeconds, IsCompleted FROM DailyTopicItem WHERE Date = @date AND SubjectName = @subjectName AND GroupTitle = @groupTitle";
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
                                        StudyTimeSeconds = Convert.ToInt32(topicReader["StudyTimeSeconds"]),
                                        IsCompleted = Convert.ToInt32(topicReader["IsCompleted"]) == 1
                                    });
                                }

                                topicGroups.Add(new TopicGroupData
                                {
                                    GroupTitle = groupTitle,
                                    TotalStudyTimeSeconds = totalStudyTimeSeconds,
                                    IsCompleted = isCompleted,
                                    Topics = topics
                                });
                            }

                            result.Add((subjectName, progress, studyTimeSeconds, topicGroups));
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
                            // DailySubject만 삭제 (오늘 할 일 목록에서만 제거)
                            using var cmd = conn.CreateCommand();
                            cmd.Transaction = transaction;
                            cmd.CommandText = "DELETE FROM DailySubject WHERE Date = @date AND SubjectName = @subjectName";
                            cmd.Parameters.AddWithValue("@date", date.ToString("yyyy-MM-dd"));
                            cmd.Parameters.AddWithValue("@subjectName", subjectName);
                            cmd.ExecuteNonQuery();

                            // 관련 DailyTopicGroup 삭제 (오늘 할 일에서만 제거)
                            using var groupCmd = conn.CreateCommand();
                            groupCmd.Transaction = transaction;
                            groupCmd.CommandText = "DELETE FROM DailyTopicGroup WHERE Date = @date AND SubjectName = @subjectName";
                            groupCmd.Parameters.AddWithValue("@date", date.ToString("yyyy-MM-dd"));
                            groupCmd.Parameters.AddWithValue("@subjectName", subjectName);
                            groupCmd.ExecuteNonQuery();

                            // 관련 DailyTopicItem 삭제 (오늘 할 일에서만 제거)
                            using var itemCmd = conn.CreateCommand();
                            itemCmd.Transaction = transaction;
                            itemCmd.CommandText = "DELETE FROM DailyTopicItem WHERE Date = @date AND SubjectName = @subjectName";
                            itemCmd.Parameters.AddWithValue("@date", date.ToString("yyyy-MM-dd"));
                            itemCmd.Parameters.AddWithValue("@subjectName", subjectName);
                            itemCmd.ExecuteNonQuery();

                            // Important: StudySession is not deleted!
                            // StudySession table stores actual measured study time and should be preserved
                            // Subject, TopicGroup, TopicItem tables are also basic structures and should be preserved

                            transaction.Commit();
                            System.Diagnostics.Debug.WriteLine($"[DB] 오늘 할 일에서 과목 '{subjectName}' 제거됨 (실제 학습시간은 보존)");
                        }
                        catch
                        {
                            transaction.Rollback();
                            throw;
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[DB] 오늘 할 일 과목 제거 오류: {ex.Message}");
                    }
                }
            });
        }
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
                            // DailySubject만 삭제 (오늘 할 일 목록 전체 초기화)
                            using var cmd = conn.CreateCommand();
                            cmd.Transaction = transaction;
                            cmd.CommandText = "DELETE FROM DailySubject WHERE Date = @date";
                            cmd.Parameters.AddWithValue("@date", date.ToString("yyyy-MM-dd"));
                            cmd.ExecuteNonQuery();

                            // DailyTopicGroup 삭제 (오늘 할 일 관련 분류 전체 제거)
                            using var groupCmd = conn.CreateCommand();
                            groupCmd.Transaction = transaction;
                            groupCmd.CommandText = "DELETE FROM DailyTopicGroup WHERE Date = @date";
                            groupCmd.Parameters.AddWithValue("@date", date.ToString("yyyy-MM-dd"));
                            groupCmd.ExecuteNonQuery();

                            // DailyTopicItem 삭제 (오늘 할 일 관련 토픽 전체 제거)
                            using var itemCmd = conn.CreateCommand();
                            itemCmd.Transaction = transaction;
                            itemCmd.CommandText = "DELETE FROM DailyTopicItem WHERE Date = @date";
                            itemCmd.Parameters.AddWithValue("@date", date.ToString("yyyy-MM-dd"));
                            itemCmd.ExecuteNonQuery();

                            // Important: StudySession, Subject, TopicGroup, TopicItem are not deleted!
                            // These are actual measured data and basic structures and should be preserved

                            transaction.Commit();
                            System.Diagnostics.Debug.WriteLine($"[DB] 해당 날짜의 모든 오늘 할 일 제거됨 (실제 학습시간은 보존): {date:yyyy-MM-dd}");
                        }
                        catch
                        {
                            transaction.Rollback();
                            throw;
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[DB] 모든 오늘 할 일 제거 오류: {ex.Message}");
                    }
                }
            });
        }
        // New method: Completely remove study data (for admin use)
        public void CompletelyRemoveSubject(string subjectName)
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
                            // Warning: This method permanently deletes all data!

                            // 1. Delete DailySubject for all dates
                            using var dailyCmd = conn.CreateCommand();
                            dailyCmd.Transaction = transaction;
                            dailyCmd.CommandText = "DELETE FROM DailySubject WHERE SubjectName = @subjectName";
                            dailyCmd.Parameters.AddWithValue("@subjectName", subjectName);
                            dailyCmd.ExecuteNonQuery();

                            // 2. Delete DailyTopicGroup for all dates
                            using var dailyGroupCmd = conn.CreateCommand();
                            dailyGroupCmd.Transaction = transaction;
                            dailyGroupCmd.CommandText = "DELETE FROM DailyTopicGroup WHERE SubjectName = @subjectName";
                            dailyGroupCmd.Parameters.AddWithValue("@subjectName", subjectName);
                            dailyGroupCmd.ExecuteNonQuery();

                            // 3. Delete DailyTopicItem for all dates
                            using var dailyItemCmd = conn.CreateCommand();
                            dailyItemCmd.Transaction = transaction;
                            dailyItemCmd.CommandText = "DELETE FROM DailyTopicItem WHERE SubjectName = @subjectName";
                            dailyItemCmd.Parameters.AddWithValue("@subjectName", subjectName);
                            dailyItemCmd.ExecuteNonQuery();

                            // 4. Delete from Subject table (CASCADE will also delete TopicGroup, TopicItem)
                            using var subjectCmd = conn.CreateCommand();
                            subjectCmd.Transaction = transaction;
                            subjectCmd.CommandText = "DELETE FROM Subject WHERE Name = @subjectName";
                            subjectCmd.Parameters.AddWithValue("@subjectName", subjectName);
                            subjectCmd.ExecuteNonQuery();

                            // 5. StudySession is not categorized by subject, so it is not deleted
                            // (Total study time is the sum of all subjects)

                            transaction.Commit();
                            System.Diagnostics.Debug.WriteLine($"[DB] 과목 '{subjectName}' 완전 삭제됨 (주의: 복구 불가!)");
                        }
                        catch
                        {
                            transaction.Rollback();
                            throw;
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[DB] 과목 완전 삭제 오류: {ex.Message}");
                    }
                }
            });
        }

        // ===== Checkbox status update methods =====
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
        // New method: Calculate actual study time per subject (based on StudySession)
        public int GetSubjectActualStudyTimeSeconds(DateTime date, string subjectName)
        {
            return ExecuteWithRetry(() =>
            {
                lock (_lockObject)
                {
                    try
                    {
                        using var conn = GetConnection();
                        conn.Open();

                        // Currently StudySession is not categorized by subject
                        // SubjectName column needs to be added to StudySession for subject page implementation

                        // Temporary: If DailySubject exists, use its value, otherwise 0
                        using var cmd = conn.CreateCommand();
                        cmd.CommandText = "SELECT COALESCE(StudyTimeSeconds, 0) FROM DailySubject WHERE Date = @date AND SubjectName = @subjectName";
                        cmd.Parameters.AddWithValue("@date", date.ToString("yyyy-MM-dd"));
                        cmd.Parameters.AddWithValue("@subjectName", subjectName);

                        var result = cmd.ExecuteScalar();
                        int studyTimeSeconds = Convert.ToInt32(result);

                        System.Diagnostics.Debug.WriteLine($"[DB] 과목 '{subjectName}' 실제 시간: {studyTimeSeconds}초");
                        return studyTimeSeconds;
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[DB] 과목 실제 시간 조회 오류: {ex.Message}");
                        return 0;
                    }
                }
            });
        }
        // Future extension: Method to be used when SubjectName column is added to StudySession table
        public int GetSubjectActualStudyTimeSecondsFromSessions(DateTime date, string subjectName)
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

                        // Will be used when StudySession table structure changes in the future
                        cmd.CommandText = @"
                    SELECT COALESCE(SUM(DurationSeconds), 0)
                    FROM StudySession
                    WHERE Date = @date AND SubjectName = @subjectName";
                        cmd.Parameters.AddWithValue("@date", date.ToString("yyyy-MM-dd"));
                        cmd.Parameters.AddWithValue("@subjectName", subjectName);

                        var result = cmd.ExecuteScalar();
                        return Convert.ToInt32(result);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[DB] StudySession 기반 과목 시간 조회 오류: {ex.Message}");
                        return 0;
                    }
                }
            });
        }
        // Actual measured daily study time per subject (based on StudySession)
        public int GetSubjectActualDailyTimeSeconds(DateTime date, string subjectName)
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

                        // Aggregate actual measured time for the subject from StudySession
                        cmd.CommandText = @"
                    SELECT COALESCE(SUM(DurationSeconds), 0)
                    FROM StudySession
                    WHERE Date = @date AND SubjectName = @subjectName";
                        cmd.Parameters.AddWithValue("@date", date.ToString("yyyy-MM-dd"));
                        cmd.Parameters.AddWithValue("@subjectName", subjectName);

                        var result = cmd.ExecuteScalar();
                        int actualTime = Convert.ToInt32(result);

                        System.Diagnostics.Debug.WriteLine($"[DB] 과목 '{subjectName}' 실제 측정 시간: {actualTime}초");
                        return actualTime;
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[DB] 과목 실제 시간 조회 오류: {ex.Message}");
                        return 0;
                    }
                }
            });
        }
        // Actual measured daily study time per topic group (based on StudySession)
        // Debug-enhanced actual measured time per topic group
        public int GetTopicGroupActualDailyTimeSeconds(DateTime date, string subjectName, string topicGroupName)
        {
            return ExecuteWithRetry(() =>
            {
                lock (_lockObject)
                {
                    try
                    {
                        using var conn = GetConnection();
                        conn.Open();

                        // 1st step: Check all category data for the subject
                        using var debugCmd = conn.CreateCommand();
                        debugCmd.CommandText = "SELECT Id, SubjectName, TopicGroupName, DurationSeconds FROM StudySession WHERE Date = @date AND SubjectName = @subjectName";
                        debugCmd.Parameters.AddWithValue("@date", date.ToString("yyyy-MM-dd"));
                        debugCmd.Parameters.AddWithValue("@subjectName", subjectName);

                        System.Diagnostics.Debug.WriteLine($"[DB] === {subjectName} 과목의 분류별 StudySession 데이터 ===");
                        using (var debugReader = debugCmd.ExecuteReader())
                        {
                            while (debugReader.Read())
                            {
                                var id = debugReader["Id"];
                                var dbSubject = debugReader["SubjectName"] ?? "NULL";
                                var dbTopic = debugReader["TopicGroupName"] ?? "NULL";
                                var duration = debugReader["DurationSeconds"];
                                System.Diagnostics.Debug.WriteLine($"[DB] ID:{id}, Subject:{dbSubject}, TopicGroup:{dbTopic}, Duration:{duration}초");
                            }
                        }

                        // 2nd step: Query for specific category time
                        using var cmd = conn.CreateCommand();
                        cmd.CommandText = @"
                    SELECT COALESCE(SUM(DurationSeconds), 0)
                    FROM StudySession
                    WHERE Date = @date AND SubjectName = @subjectName AND TopicGroupName = @topicGroupName";
                        cmd.Parameters.AddWithValue("@date", date.ToString("yyyy-MM-dd"));
                        cmd.Parameters.AddWithValue("@subjectName", subjectName);
                        cmd.Parameters.AddWithValue("@topicGroupName", topicGroupName);

                        var result = cmd.ExecuteScalar();
                        int actualTime = Convert.ToInt32(result);

                        System.Diagnostics.Debug.WriteLine($"[DB] ✅ 분류 '{subjectName}>{topicGroupName}' {date:yyyy-MM-dd} 실제 측정 시간: {actualTime}초");

                        // 3rd step: Fallback logic if TopicGroupName is NULL
                        if (actualTime == 0)
                        {
                            System.Diagnostics.Debug.WriteLine($"[DB] ⚠️ '{topicGroupName}' 실제 시간이 0초입니다. DailyTopicGroup에서 확인합니다.");

                            // Fallback query from DailyTopicGroup
                            using var fallbackCmd = conn.CreateCommand();
                            fallbackCmd.CommandText = "SELECT COALESCE(TotalStudyTimeSeconds, 0) FROM DailyTopicGroup WHERE Date = @date AND SubjectName = @subjectName AND GroupTitle = @groupTitle";
                            fallbackCmd.Parameters.AddWithValue("@date", date.ToString("yyyy-MM-dd"));
                            fallbackCmd.Parameters.AddWithValue("@subjectName", subjectName);
                            fallbackCmd.Parameters.AddWithValue("@groupTitle", topicGroupName);

                            var fallbackResult = fallbackCmd.ExecuteScalar();
                            int fallbackTime = Convert.ToInt32(fallbackResult);

                            System.Diagnostics.Debug.WriteLine($"[DB] 📋 DailyTopicGroup에서 '{topicGroupName}' 시간: {fallbackTime}초");
                            return fallbackTime;
                        }

                        return actualTime;
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[DB] ❌ 분류 실제 시간 조회 오류: {ex.Message}");
                        return 0;
                    }
                }
            });
        }

        // Restore subject to Daily after drag & drop deletion
        public void RestoreSubjectToDaily(DateTime date, string subjectName)
        {
            ExecuteWithRetry(() =>
            {
                lock (_lockObject)
                {
                    try
                    {
                        // 1. Check if DailySubject already exists
                        var existingTime = GetDailySubjectStudyTimeSeconds(date, subjectName);

                        if (existingTime == 0)
                        {
                            // 2. Get cumulative time from Subject table
                            var totalTime = GetSubjectTotalStudyTimeSeconds(subjectName);

                            // 3. Temporarily set some time as today's time (for testing)
                            var todayTime = Math.Min(3600, totalTime); // Max 1 hour

                            // 4. Restore to DailySubject
                            if (todayTime > 0)
                            {
                                SaveDailySubject(date, subjectName, 0.0, todayTime, 0);
                                System.Diagnostics.Debug.WriteLine($"[DB] 과목 '{subjectName}' 오늘 할 일에 복원됨: {todayTime}초");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[DB] 과목 복원 오류: {ex.Message}");
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

        public void CleanupDuplicateData(DateTime date)
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
                            // 1. 중복 데이터 확인
                            using var checkCmd = conn.CreateCommand();
                            checkCmd.Transaction = transaction;
                            checkCmd.CommandText = "SELECT COUNT(*) FROM DailySubject WHERE Date = @date";
                            checkCmd.Parameters.AddWithValue("@date", date.ToString("yyyy-MM-dd"));
                            var count = Convert.ToInt32(checkCmd.ExecuteScalar());

                            System.Diagnostics.Debug.WriteLine($"[DB] 정리 전 DailySubject 개수: {count}개");

                            // 2. 중복 데이터 삭제 (최신 것만 남기고)
                            using var cleanupCmd = conn.CreateCommand();
                            cleanupCmd.Transaction = transaction;
                            cleanupCmd.CommandText = @"
                                DELETE FROM DailySubject
                                WHERE Date = @date
                                AND Id NOT IN (
                                    SELECT MAX(Id)
                                    FROM DailySubject
                                    WHERE Date = @date
                                    GROUP BY SubjectName
                                )";
                            cleanupCmd.Parameters.AddWithValue("@date", date.ToString("yyyy-MM-dd"));
                            var deletedCount = cleanupCmd.ExecuteNonQuery();

                            // 3. DailyTopicGroup도 정리
                            using var cleanupGroupCmd = conn.CreateCommand();
                            cleanupGroupCmd.Transaction = transaction;
                            cleanupGroupCmd.CommandText = @"
                                DELETE FROM DailyTopicGroup
                                WHERE Date = @date
                                AND Id NOT IN (
                                    SELECT MAX(Id)
                                    FROM DailyTopicGroup
                                    WHERE Date = @date
                                    GROUP BY SubjectName, GroupTitle
                                )";
                            cleanupGroupCmd.Parameters.AddWithValue("@date", date.ToString("yyyy-MM-dd"));
                            var deletedGroupCount = cleanupGroupCmd.ExecuteNonQuery();

                            // 4. DailyTopicItem도 정리
                            using var cleanupItemCmd = conn.CreateCommand();
                            cleanupItemCmd.Transaction = transaction;
                            cleanupItemCmd.CommandText = @"
                                DELETE FROM DailyTopicItem
                                WHERE Date = @date
                                AND Id NOT IN (
                                    SELECT MAX(Id)
                                    FROM DailyTopicItem
                                    WHERE Date = @date
                                    GROUP BY SubjectName, GroupTitle, TopicName
                                )";
                            cleanupItemCmd.Parameters.AddWithValue("@date", date.ToString("yyyy-MM-dd"));
                            var deletedItemCount = cleanupItemCmd.ExecuteNonQuery();

                            transaction.Commit();

                            System.Diagnostics.Debug.WriteLine($"[DB] 정리 완료 - 삭제된 DailySubject: {deletedCount}개, TopicGroup: {deletedGroupCount}개, TopicItem: {deletedItemCount}개");
                        }
                        catch
                        {
                            transaction.Rollback();
                            throw;
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[DB] 데이터 정리 오류: {ex.Message}");
                    }
                }
            });
        }

        // ===== Compatibility methods =====

        // Removed duplicate GetTotalStudyTimeMinutes methods, keeping single version
        public int GetTotalStudyTimeMinutes(DateTime date)
        {
            return GetTotalStudyTimeSeconds(date) / 60;
        }

        public int GetTotalStudyTimeMinutes()
        {
            return GetTotalStudyTimeSeconds() / 60;
        }

        // Existing compatibility methods (marked Obsolete)
        [Obsolete("Use GetTotalAllSubjectsStudyTimeSeconds instead")]
        public int GetTotalAllSubjectsStudyTime()
        {
            return GetTotalAllSubjectsStudyTimeSeconds();
        }

        [Obsolete("Use GetSubjectTotalStudyTimeSeconds instead")]
        public int GetSubjectTotalStudyTime(string subjectName)
        {
            return GetSubjectTotalStudyTimeSeconds(subjectName);
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
                    cmd.CommandText = "SELECT Id, Name, TotalStudyTimeSeconds FROM Subject ORDER BY TotalStudyTimeSeconds DESC";
                    using var reader = cmd.ExecuteReader();

                    while (reader.Read())
                    {
                        var subjectId = Convert.ToInt32(reader["Id"]);
                        var subjectName = reader["Name"].ToString();
                        var totalStudyTimeSeconds = Convert.ToInt32(reader["TotalStudyTimeSeconds"]);

                        var subjectVM = new SubjectGroupViewModel
                        {
                            SubjectId = subjectId,
                            SubjectName = subjectName,
                            TotalStudyTimeSeconds = totalStudyTimeSeconds,
                            TopicGroups = new ObservableCollection<TopicGroupViewModel>()
                        };

                        result.Add(subjectVM);
                    }

                    return result;
                }
            });
        }

        // GetDailySubjectStudyTimeSeconds method (using correct column name)
        public int GetDailySubjectStudyTimeSeconds(DateTime date, string subjectName)
        {
            return ExecuteWithRetry(() =>
            {
                lock (_lockObject)
                {
                    using var conn = GetConnection();
                    conn.Open();
                    using var cmd = conn.CreateCommand();

                    cmd.CommandText = "SELECT COALESCE(StudyTimeSeconds, 0) FROM DailySubject WHERE Date = @date AND SubjectName = @subjectName";
                    cmd.Parameters.AddWithValue("@date", date.ToString("yyyy-MM-dd"));
                    cmd.Parameters.AddWithValue("@subjectName", subjectName);

                    var result = cmd.ExecuteScalar();
                    int studyTimeSeconds = Convert.ToInt32(result);

                    System.Diagnostics.Debug.WriteLine($"[DB] {date:yyyy-MM-dd} 과목 '{subjectName}' 오늘 학습시간: {studyTimeSeconds}초");
                    return studyTimeSeconds;
                }
            });
        }

        // GetDailyTopicGroupStudyTimeSeconds method (using correct column name)
        public int GetDailyTopicGroupStudyTimeSeconds(DateTime date, string subjectName, string groupTitle)
        {
            return ExecuteWithRetry(() =>
            {
                lock (_lockObject)
                {
                    using var conn = GetConnection();
                    conn.Open();
                    using var cmd = conn.CreateCommand();

                    cmd.CommandText = "SELECT COALESCE(TotalStudyTimeSeconds, 0) FROM DailyTopicGroup WHERE Date = @date AND SubjectName = @subjectName AND GroupTitle = @groupTitle";
                    cmd.Parameters.AddWithValue("@date", date.ToString("yyyy-MM-dd"));
                    cmd.Parameters.AddWithValue("@subjectName", subjectName);
                    cmd.Parameters.AddWithValue("@groupTitle", groupTitle);

                    var result = cmd.ExecuteScalar();
                    int studyTimeSeconds = Convert.ToInt32(result);

                    System.Diagnostics.Debug.WriteLine($"[DB] {date:yyyy-MM-dd} 분류 '{groupTitle}' 오늘 학습시간: {studyTimeSeconds}초");
                    return studyTimeSeconds;
                }
            });
        }

        // IDisposable implementation (prevent memory leaks)
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

        // -----------------------------------------------------
        // Notea 프로젝트에서 가져온 스키마 업데이트 및 유틸리티 메서드 구현
        // -----------------------------------------------------

        // Database Schema Update for Heading Level
        private void UpdateSchemaForHeadingLevel(SQLiteConnection conn)
        {
            try
            {
                // category 테이블에 level 컬럼 추가 (없으면)
                string checkLevelColumn = @"
                    SELECT COUNT(*) as count
                    FROM pragma_table_info('category')
                    WHERE name='level'";

                using (var cmd = new SQLiteCommand(checkLevelColumn, conn))
                {
                    if (Convert.ToInt32(cmd.ExecuteScalar()) == 0)
                    {
                        string addLevelColumn = @"ALTER TABLE category ADD COLUMN level INTEGER DEFAULT 1";
                        using (var addCmd = new SQLiteCommand(addLevelColumn, conn))
                        {
                            addCmd.ExecuteNonQuery();
                        }
                        System.Diagnostics.Debug.WriteLine("[DB] category.level 컬럼 추가됨");
                    }
                }

                // parentCategoryId 컬럼 추가 (계층 구조를 위해)
                string checkParentColumn = @"
                    SELECT COUNT(*) as count
                    FROM pragma_table_info('category')
                    WHERE name='parentCategoryId'";

                using (var cmd = new SQLiteCommand(checkParentColumn, conn))
                {
                    if (Convert.ToInt32(cmd.ExecuteScalar()) == 0)
                    {
                        string addParentColumn = @"ALTER TABLE category ADD COLUMN parentCategoryId INTEGER DEFAULT NULL";
                        using (var addCmd = new SQLiteCommand(addParentColumn, conn))
                        {
                            addCmd.ExecuteNonQuery();
                        }
                        System.Diagnostics.Debug.WriteLine("[DB] category.parentCategoryId 컬럼 추가됨");
                    }
                }

                System.Diagnostics.Debug.WriteLine("[DB] 헤딩 레벨 스키마 업데이트 완료");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DB ERROR] 헤딩 레벨 스키마 업데이트 실패: {ex.Message}");
            }
        }

        // Database Schema Update for Image Support
        private void UpdateSchemaForImageSupport(SQLiteConnection conn)
        {
            try
            {
                // noteContent 테이블에 imageUrl 컬럼 추가
                string checkImageColumn = @"
                    SELECT COUNT(*) as count
                    FROM pragma_table_info('noteContent')
                    WHERE name='imageUrl'";

                using (var cmd = new SQLiteCommand(checkImageColumn, conn))
                {
                    if (Convert.ToInt32(cmd.ExecuteScalar()) == 0)
                    {
                        string addImageColumn = @"ALTER TABLE noteContent ADD COLUMN imageUrl VARCHAR DEFAULT NULL";
                        using (var addCmd = new SQLiteCommand(addImageColumn, conn))
                        {
                            addCmd.ExecuteNonQuery();
                        }
                        System.Diagnostics.Debug.WriteLine("[DB] noteContent.imageUrl 컬럼 추가됨");
                    }
                }

                // noteContent 테이블에 contentType 컬럼 추가 (text/image 구분)
                string checkTypeColumn = @"
                    SELECT COUNT(*) as count
                    FROM pragma_table_info('noteContent')
                    WHERE name='contentType'";

                using (var cmd = new SQLiteCommand(checkTypeColumn, conn))
                {
                    if (Convert.ToInt32(cmd.ExecuteScalar()) == 0)
                    {
                        string addTypeColumn = @"ALTER TABLE noteContent ADD COLUMN contentType VARCHAR DEFAULT 'text'";
                        using (var addCmd = new SQLiteCommand(addTypeColumn, conn))
                        {
                            addCmd.ExecuteNonQuery();
                        }
                        System.Diagnostics.Debug.WriteLine("[DB] noteContent.contentType 컬럼 추가됨");
                    }
                }

                System.Diagnostics.Debug.WriteLine("[DB] 이미지 지원 스키마 업데이트 완료");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DB ERROR] 이미지 지원 스키마 업데이트 실패: {ex.Message}");
            }
        }

        // Database Schema Update for Monthly Comment (이전에 누락되었던 메서드 구현)
        private void UpdateSchemaForMonthlyComment(SQLiteConnection conn)
        {
            try
            {
                // monthlyComment 테이블 생성
                string createTableQuery = @"
                    CREATE TABLE IF NOT EXISTS monthlyComment (
                        commentId INTEGER PRIMARY KEY AUTOINCREMENT,
                        monthDate DATETIME NOT NULL,
                        comment VARCHAR NULL,
                        UNIQUE(monthDate)
                    )";

                using (var cmd = new SQLiteCommand(createTableQuery, conn))
                {
                    cmd.ExecuteNonQuery();
                }
                System.Diagnostics.Debug.WriteLine("[DB] monthlyComment 테이블 생성/확인 완료");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DB ERROR] monthlyComment 스키마 업데이트 실패: {ex.Message}");
            }
        }

        // Test connection method
        public bool TestConnection()
        {
            try
            {
                using (var connection = GetConnection())
                {
                    connection.Open();
                    System.Diagnostics.Debug.WriteLine($"DB 연결 성공: {_dbPath}");
                    return true;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"DB 연결 실패: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"시도한 경로: {_dbPath}");
                return false;
            }
        }

        // Execute SELECT queries
        public DataTable ExecuteSelect(string query)
        {
            return ExecuteWithRetry(() =>
            {
                var dt = new DataTable();
                using (var connection = GetConnection())
                {
                    connection.Open();
                    using (var command = new SQLiteCommand(query, connection))
                    using (var adapter = new SQLiteDataAdapter(command))
                    {
                        adapter.Fill(dt);
                    }
                }
                System.Diagnostics.Debug.WriteLine($"SELECT 쿼리 실행 성공. 반환된 행: {dt.Rows.Count}");
                return dt;
            });
        }

        // Execute INSERT, UPDATE, DELETE queries
        public int ExecuteNonQuery(string query)
        {
            return ExecuteWithRetry(() =>
            {
                int result = 0;
                using (var connection = GetConnection())
                {
                    connection.Open();
                    using (var command = new SQLiteCommand(query, connection))
                    {
                        result = command.ExecuteNonQuery();
                    }
                }
                System.Diagnostics.Debug.WriteLine($"쿼리 실행 성공. 영향받은 행: {result}");
                return result;
            });
        }

        public void CheckTableStructure()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("=== 데이터베이스 테이블 구조 확인 ===");
                string query = @"
                    SELECT sql FROM sqlite_master
                    WHERE type='table' AND name IN ('category', 'noteContent', 'Subject', 'time', 'monthlyEvent', 'Todo', 'Note', 'TopicGroup', 'TopicItem', 'StudySession', 'DailySubject', 'DailyTopicGroup', 'DailyTopicItem');";

                var result = ExecuteSelect(query);
                foreach (DataRow row in result.Rows)
                {
                    System.Diagnostics.Debug.WriteLine($"[DB SCHEMA] {row["sql"]}");
                }

                // noteContent 테이블의 데이터 확인
                query = "SELECT COUNT(*) as count FROM noteContent";
                result = ExecuteSelect(query);
                System.Diagnostics.Debug.WriteLine($"[DB] noteContent 테이블의 행 수: {result.Rows[0]["count"]}");

                // category 테이블의 데이터 확인
                query = "SELECT * FROM category";
                result = ExecuteSelect(query);
                System.Diagnostics.Debug.WriteLine($"[DB] category 테이블 내용:");
                foreach (DataRow row in result.Rows)
                {
                    System.Diagnostics.Debug.WriteLine($"  CategoryId: {row["categoryId"]}, Title: {row["title"]}, SubjectId: {row["subJectId"]}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DB ERROR] 테이블 구조 확인 실패: {ex.Message}");
            }
        }

        public void DebugPrintAllData(int subjectId)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("=== 데이터베이스 전체 내용 ===");

                // 카테고리 출력
                string categoryQuery = $@"
                    SELECT categoryId, title, displayOrder, level, parentCategoryId
                    FROM category
                    WHERE subJectId = {subjectId}
                    ORDER BY displayOrder";

                var categoryResult = ExecuteSelect(categoryQuery);
                System.Diagnostics.Debug.WriteLine($"[카테고리] 총 {categoryResult.Rows.Count}개");
                foreach (DataRow row in categoryResult.Rows)
                {
                    System.Diagnostics.Debug.WriteLine($"  ID: {row["categoryId"]}, " +
                                    $"Title: '{row["title"]}', " +
                                    $"Order: {row["displayOrder"]}, " +
                                    $"Level: {row["level"]}, " +
                                    $"ParentId: {(row["parentCategoryId"] == DBNull.Value ? "NULL" : row["parentCategoryId"])}");
                }

                // 텍스트 내용 출력
                string textQuery = $@"
                    SELECT textId, content, categoryId, displayOrder
                    FROM noteContent
                    WHERE subJectId = {subjectId}
                    ORDER BY displayOrder";

                var textResult = ExecuteSelect(textQuery);
                System.Diagnostics.Debug.WriteLine($"\n[텍스트] 총 {textResult.Rows.Count}개");
                foreach (DataRow row in textResult.Rows)
                {
                    System.Diagnostics.Debug.WriteLine($"  ID: {row["textId"]}, " +
                                    $"CategoryId: {row["categoryId"]}, " +
                                    $"Order: {row["displayOrder"]}, " +
                                    $"Content: '{row["content"]?.ToString().Substring(0, Math.Min(50, row["content"]?.ToString().Length ?? 0))}'...");
                }

                // 카테고리별 텍스트 개수
                string countQuery = $@"
                    SELECT c.categoryId, c.title, COUNT(n.textId) as textCount
                    FROM category c
                    LEFT JOIN noteContent n ON c.categoryId = n.categoryId
                    WHERE c.subJectId = {subjectId}
                    GROUP BY c.categoryId, c.title
                    ORDER BY c.displayOrder";

                var countResult = ExecuteSelect(countQuery);
                System.Diagnostics.Debug.WriteLine($"\n[카테고리별 텍스트 개수]");
                foreach (DataRow row in countResult.Rows)
                {
                    System.Diagnostics.Debug.WriteLine($"  카테고리 '{row["title"]}' (ID: {row["categoryId"]}): {row["textCount"]}개");
                }

                System.Diagnostics.Debug.WriteLine("========================");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ERROR] DebugPrintAllData: {ex.Message}");
            }
        }

        public void VerifyDatabaseIntegrity(int subjectId)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("=== 데이터베이스 무결성 검증 ===");

                // 1. 고아 noteContent 찾기
                string orphanQuery = $@"
                    SELECT n.textId, n.content, n.categoryId
                    FROM noteContent n
                    LEFT JOIN category c ON n.categoryId = c.categoryId
                    WHERE n.subJectId = {subjectId} AND c.categoryId IS NULL";

                var orphanResult = ExecuteSelect(orphanQuery);
                if (orphanResult.Rows.Count > 0)
                {
                    System.Diagnostics.Debug.WriteLine($"[DB ERROR] 고아 noteContent 발견: {orphanResult.Rows.Count}개");
                    foreach (DataRow row in orphanResult.Rows)
                    {
                        System.Diagnostics.Debug.WriteLine($"  TextId: {row["textId"]}, CategoryId: {row["categoryId"]}");
                    }
                }

                // 2. DisplayOrder 중복 검사
                string duplicateQuery = $@"
                    SELECT displayOrder, COUNT(*) as cnt
                    FROM (
                        SELECT displayOrder FROM category WHERE subJectId = {subjectId}
                        UNION ALL
                        SELECT displayOrder FROM noteContent WHERE subJectId = {subjectId}
                    )
                    GROUP BY displayOrder
                    HAVING COUNT(*) > 1";

                var duplicateResult = ExecuteSelect(duplicateQuery);
                if (duplicateResult.Rows.Count > 0)
                {
                    System.Diagnostics.Debug.WriteLine($"[DB ERROR] DisplayOrder 중복 발견:");
                    foreach (DataRow row in duplicateResult.Rows)
                    {
                        System.Diagnostics.Debug.WriteLine($"  DisplayOrder: {row["displayOrder"]}, Count: {row["cnt"]}");
                    }
                }

                // 3. 이미지 파일 검증
                string imageQuery = $@"
                    SELECT textId, imageUrl
                    FROM noteContent
                    WHERE subJectId = {subjectId} AND contentType = 'image' AND imageUrl IS NOT NULL";

                var imageResult = ExecuteSelect(imageQuery);
                foreach (DataRow row in imageResult.Rows)
                {
                    string imageUrl = row["imageUrl"].ToString();
                    string fullPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, imageUrl);
                    if (!File.Exists(fullPath))
                    {
                        System.Diagnostics.Debug.WriteLine($"[DB ERROR] 이미지 파일 없음: TextId={row["textId"]}, Path={imageUrl}");
                    }
                }

                System.Diagnostics.Debug.WriteLine("=== 검증 완료 ===");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DB ERROR] 무결성 검증 실패: {ex.Message}");
            }
        }

        // DB path getter
        public string GetDatabasePath() => _dbPath;
    }

    // ===== Data Transfer Classes =====
    public class TopicGroupData
    {
        public string GroupTitle { get; set; } = string.Empty;
        public int TotalStudyTimeSeconds { get; set; }
        public bool IsCompleted { get; set; }
        public List<TopicItemData> Topics { get; set; } = new();
    }

    public class TopicItemData
    {
        public string Name { get; set; } = string.Empty;
        public double Progress { get; set; }
        public int StudyTimeSeconds { get; set; }
        public bool IsCompleted { get; set; }
    }
}