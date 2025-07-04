﻿using SP.Modules.Common.Models;
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
            var connectionString = $"Data Source={_dbPath};Version=3;Pooling=true;Max Pool Size=100;Timeout=30;Journal Mode=WAL;";
            return new SQLiteConnection(connectionString);
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
                        pragmaCmd.CommandText = "PRAGMA journal_mode=WAL; PRAGMA synchronous=NORMAL; PRAGMA cache_size=10000; PRAGMA temp_store=memory;";
                        pragmaCmd.ExecuteNonQuery();

                        var cmd = conn.CreateCommand();

                        // Note 테이블
                        cmd.CommandText = @"
                            CREATE TABLE IF NOT EXISTS Note (
                                NoteId INTEGER PRIMARY KEY AUTOINCREMENT,
                                Content TEXT NOT NULL,
                                CreatedAt TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
                                UpdatedAt TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP
                            );";
                        cmd.ExecuteNonQuery();

                        // Comment 테이블
                        cmd.CommandText = @"
                            CREATE TABLE IF NOT EXISTS Comment (
                                Date TEXT PRIMARY KEY,
                                Text TEXT NOT NULL
                            );";
                        cmd.ExecuteNonQuery();

                        // Todo 테이블
                        cmd.CommandText = @"
                            CREATE TABLE IF NOT EXISTS Todo (
                                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                                Date TEXT NOT NULL,
                                Title TEXT NOT NULL,
                                IsCompleted INTEGER NOT NULL DEFAULT 0
                            );";
                        cmd.ExecuteNonQuery();

                        // ✅ Subject 테이블 (초단위)
                        cmd.CommandText = @"
                            CREATE TABLE IF NOT EXISTS Subject (
                                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                                Name TEXT NOT NULL UNIQUE,
                                TotalStudyTimeSeconds INTEGER NOT NULL DEFAULT 0
                            );";
                        cmd.ExecuteNonQuery();

                        // ✅ TopicGroup 테이블 (초단위)
                        cmd.CommandText = @"
                            CREATE TABLE IF NOT EXISTS TopicGroup (
                                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                                SubjectId INTEGER NOT NULL,
                                Name TEXT NOT NULL,
                                TotalStudyTimeSeconds INTEGER NOT NULL DEFAULT 0,
                                FOREIGN KEY (SubjectId) REFERENCES Subject(Id) ON DELETE CASCADE
                            );";
                        cmd.ExecuteNonQuery();

                        // TopicItem 테이블
                        cmd.CommandText = @"
                            CREATE TABLE IF NOT EXISTS TopicItem (
                                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                                TopicGroupId INTEGER NOT NULL,
                                Content TEXT NOT NULL,
                                CreatedAt TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
                                FOREIGN KEY (TopicGroupId) REFERENCES TopicGroup(Id) ON DELETE CASCADE
                            );";
                        cmd.ExecuteNonQuery();

                        // StudySession 테이블
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

                        // ✅ DailySubject 테이블 (초단위)
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

                        // ✅ DailyTopicGroup 테이블 (초단위)
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

                        // ✅ DailyTopicItem 테이블 (초단위)
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

        // ✅ 수정: LoadSubjectsWithGroups 메소드 (초단위 컬럼 사용)
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
                            TotalStudyTimeSeconds = totalStudyTimeSeconds, // ✅ 초단위 사용
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

        // ✅ 수정: LoadTopicGroupsForSubject 메소드 (초단위 컬럼 사용)
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
                    TotalStudyTimeSeconds = groupStudyTimeSeconds, // ✅ 초단위 사용
                    ParentSubjectName = subject.SubjectName,
                    Topics = new ObservableCollection<SP.Modules.Subjects.Models.TopicItem>()
                };

                topicGroup.SetSubjectTotalTime(subject.TotalStudyTimeSeconds); // ✅ 초단위 전달

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
                    StudyTimeSeconds = 0 // ✅ 초단위 사용
                };

                topicGroup.Topics.Add(topicItem);
            }

            System.Diagnostics.Debug.WriteLine($"[DB] TopicGroup '{topicGroup.GroupTitle}'에 {topicGroup.Topics.Count}개 TopicItem 로드됨");
        }


        // ✅ 먼저 5개 인수를 받는 메서드 정의
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

        // ✅ 기존 3개 인수 메서드 (호환성 유지) - 5개 인수 메서드 호출
        public void SaveStudySession(DateTime startTime, DateTime endTime, int durationSeconds)
        {
            SaveStudySession(startTime, endTime, durationSeconds, null, null);
        }

        // ✅ 수정: GetTotalStudyTimeSeconds(DateTime date) 오버로드 추가
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
                                    topicCmd.Parameters.AddWithValue("@subjectName", subjectName);
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

        public List<(string SubjectName, double Progress, int StudyTimeSeconds)> GetDailySubjects(DateTime date)
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
                        cmd.CommandText = "SELECT SubjectName, Progress, StudyTimeSeconds FROM DailySubject WHERE Date = @date ORDER BY DisplayOrder";
                        cmd.Parameters.AddWithValue("@date", date.ToString("yyyy-MM-dd"));

                        using var reader = cmd.ExecuteReader();
                        while (reader.Read())
                        {
                            result.Add((
                                reader["SubjectName"].ToString(),
                                Convert.ToDouble(reader["Progress"]),
                                Convert.ToInt32(reader["StudyTimeSeconds"])
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
                            // ✅ DailySubject만 삭제 (오늘 할 일 목록에서만 제거)
                            using var cmd = conn.CreateCommand();
                            cmd.Transaction = transaction;
                            cmd.CommandText = "DELETE FROM DailySubject WHERE Date = @date AND SubjectName = @subjectName";
                            cmd.Parameters.AddWithValue("@date", date.ToString("yyyy-MM-dd"));
                            cmd.Parameters.AddWithValue("@subjectName", subjectName);
                            cmd.ExecuteNonQuery();

                            // ✅ 관련 DailyTopicGroup 삭제 (오늘 할 일에서만 제거)
                            using var groupCmd = conn.CreateCommand();
                            groupCmd.Transaction = transaction;
                            groupCmd.CommandText = "DELETE FROM DailyTopicGroup WHERE Date = @date AND SubjectName = @subjectName";
                            groupCmd.Parameters.AddWithValue("@date", date.ToString("yyyy-MM-dd"));
                            groupCmd.Parameters.AddWithValue("@subjectName", subjectName);
                            groupCmd.ExecuteNonQuery();

                            // ✅ 관련 DailyTopicItem 삭제 (오늘 할 일에서만 제거)
                            using var itemCmd = conn.CreateCommand();
                            itemCmd.Transaction = transaction;
                            itemCmd.CommandText = "DELETE FROM DailyTopicItem WHERE Date = @date AND SubjectName = @subjectName";
                            itemCmd.Parameters.AddWithValue("@date", date.ToString("yyyy-MM-dd"));
                            itemCmd.Parameters.AddWithValue("@subjectName", subjectName);
                            itemCmd.ExecuteNonQuery();

                            // ⚠️ 중요: StudySession은 삭제하지 않음!
                            // StudySession 테이블은 실제 측정된 학습시간이므로 보존
                            // Subject, TopicGroup, TopicItem 테이블도 기본 구조이므로 보존

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
                            // ✅ DailySubject만 삭제 (오늘 할 일 목록 전체 초기화)
                            using var cmd = conn.CreateCommand();
                            cmd.Transaction = transaction;
                            cmd.CommandText = "DELETE FROM DailySubject WHERE Date = @date";
                            cmd.Parameters.AddWithValue("@date", date.ToString("yyyy-MM-dd"));
                            cmd.ExecuteNonQuery();

                            // ✅ DailyTopicGroup 삭제 (오늘 할 일 관련 분류 전체 제거)
                            using var groupCmd = conn.CreateCommand();
                            groupCmd.Transaction = transaction;
                            groupCmd.CommandText = "DELETE FROM DailyTopicGroup WHERE Date = @date";
                            groupCmd.Parameters.AddWithValue("@date", date.ToString("yyyy-MM-dd"));
                            groupCmd.ExecuteNonQuery();

                            // ✅ DailyTopicItem 삭제 (오늘 할 일 관련 토픽 전체 제거)
                            using var itemCmd = conn.CreateCommand();
                            itemCmd.Transaction = transaction;
                            itemCmd.CommandText = "DELETE FROM DailyTopicItem WHERE Date = @date";
                            itemCmd.Parameters.AddWithValue("@date", date.ToString("yyyy-MM-dd"));
                            itemCmd.ExecuteNonQuery();

                            // ⚠️ 중요: StudySession, Subject, TopicGroup, TopicItem은 삭제하지 않음!
                            // 이들은 실제 측정 데이터 및 기본 구조이므로 보존

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
        // ✅ 새로운 메소드: 실제 학습시간까지 완전 삭제 (관리자 기능용)
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
                            // ⚠️ 경고: 이 메소드는 모든 데이터를 완전 삭제합니다!

                            // 1. 모든 날짜의 DailySubject 삭제
                            using var dailyCmd = conn.CreateCommand();
                            dailyCmd.Transaction = transaction;
                            dailyCmd.CommandText = "DELETE FROM DailySubject WHERE SubjectName = @subjectName";
                            dailyCmd.Parameters.AddWithValue("@subjectName", subjectName);
                            dailyCmd.ExecuteNonQuery();

                            // 2. 모든 날짜의 DailyTopicGroup 삭제
                            using var dailyGroupCmd = conn.CreateCommand();
                            dailyGroupCmd.Transaction = transaction;
                            dailyGroupCmd.CommandText = "DELETE FROM DailyTopicGroup WHERE SubjectName = @subjectName";
                            dailyGroupCmd.Parameters.AddWithValue("@subjectName", subjectName);
                            dailyGroupCmd.ExecuteNonQuery();

                            // 3. 모든 날짜의 DailyTopicItem 삭제
                            using var dailyItemCmd = conn.CreateCommand();
                            dailyItemCmd.Transaction = transaction;
                            dailyItemCmd.CommandText = "DELETE FROM DailyTopicItem WHERE SubjectName = @subjectName";
                            dailyItemCmd.Parameters.AddWithValue("@subjectName", subjectName);
                            dailyItemCmd.ExecuteNonQuery();

                            // 4. Subject 테이블에서 삭제 (CASCADE로 TopicGroup, TopicItem도 자동 삭제)
                            using var subjectCmd = conn.CreateCommand();
                            subjectCmd.Transaction = transaction;
                            subjectCmd.CommandText = "DELETE FROM Subject WHERE Name = @subjectName";
                            subjectCmd.Parameters.AddWithValue("@subjectName", subjectName);
                            subjectCmd.ExecuteNonQuery();

                            // 5. StudySession은 과목별로 분류되어 있지 않으므로 삭제하지 않음
                            //    (전체 학습시간은 모든 과목의 총합이므로)

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

        // ===== 체크박스 상태 업데이트 메소드들 =====
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
        // ✅ 새로운 메소드: 과목별 실제 측정 시간 계산 (StudySession 기반)
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

                        // ⚠️ 현재는 StudySession이 과목별로 분류되어 있지 않음
                        // 추후 과목페이지 구현시 StudySession에 SubjectName 컬럼 추가 필요

                        // 임시 방법: DailySubject가 있으면 그 값, 없으면 0
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
        // ✅ 향후 확장: StudySession 테이블에 SubjectName 추가시 사용할 메소드
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

                        // ✅ 추후 StudySession 테이블 구조 변경시 사용
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
        // ✅ 과목별 실제 측정된 일일 학습시간 조회 (StudySession 기반)
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

                        // StudySession에서 해당 과목의 실제 측정 시간 집계
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
        // ✅ 분류별 실제 측정된 일일 학습시간 조회 (StudySession 기반)
        // ✅ 디버그 강화된 분류별 실제 측정 시간 조회
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

                        // ✅ 1단계: 해당 과목의 모든 분류 데이터 확인
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

                        // ✅ 2단계: 특정 분류 시간 조회
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

                        // ✅ 3단계: TopicGroupName이 NULL인 경우 대체 로직
                        if (actualTime == 0)
                        {
                            System.Diagnostics.Debug.WriteLine($"[DB] ⚠️ '{topicGroupName}' 실제 시간이 0초입니다. DailyTopicGroup에서 확인합니다.");

                            // DailyTopicGroup에서 백업 조회
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

        // ✅ 드래그&드롭 삭제 후 과목이 다시 추가될 때 기존 시간 복원
        public void RestoreSubjectToDaily(DateTime date, string subjectName)
        {
            ExecuteWithRetry(() =>
            {
                lock (_lockObject)
                {
                    try
                    {
                        // 1. 기존에 DailySubject가 있는지 확인
                        var existingTime = GetDailySubjectStudyTimeSeconds(date, subjectName);

                        if (existingTime == 0)
                        {
                            // 2. Subject 테이블에서 해당 과목의 누적 시간 가져오기
                            var totalTime = GetSubjectTotalStudyTimeSeconds(subjectName);

                            // 3. 임시로 일부 시간을 오늘 시간으로 설정 (테스트용)
                            var todayTime = Math.Min(3600, totalTime); // 최대 1시간

                            // 4. DailySubject에 복원
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

        // ===== 호환성 메소드들 =====

        // ✅ 제거: 중복된 GetTotalStudyTimeMinutes 메소드들 제거하고 단일 버전만 유지
        public int GetTotalStudyTimeMinutes(DateTime date)
        {
            return GetTotalStudyTimeSeconds(date) / 60;
        }

        public int GetTotalStudyTimeMinutes()
        {
            return GetTotalStudyTimeSeconds() / 60;
        }

        // ✅ 기존 호환성 메소드들 (Obsolete 표시)
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

        // ✅ 수정: GetDailySubjectStudyTimeSeconds 메소드 (올바른 컬럼명 사용)
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

        // ✅ 수정: GetDailyTopicGroupStudyTimeSeconds 메소드 (올바른 컬럼명 사용)
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

    // ===== 데이터 전송용 클래스들 =====
    public class TopicGroupData
    {
        public string GroupTitle { get; set; } = string.Empty;
        public int TotalStudyTimeSeconds { get; set; } // ✅ 초단위
        public bool IsCompleted { get; set; }
        public List<TopicItemData> Topics { get; set; } = new();
    }

    public class TopicItemData
    {
        public string Name { get; set; } = string.Empty;
        public double Progress { get; set; }
        public int StudyTimeSeconds { get; set; } // ✅ 초단위
        public bool IsCompleted { get; set; }
    }
}