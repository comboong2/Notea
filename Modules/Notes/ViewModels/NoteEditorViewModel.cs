﻿using Microsoft.Data.Sqlite;
using SP.Modules.Common.Helpers;
using SP.Modules.Notes.Models;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Transactions;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using static SP.Modules.Notes.ViewModels.MarkdownLineViewModel;

namespace SP.Modules.Notes.ViewModels
{
    public class NoteEditorViewModel : INotifyPropertyChanged
    {

        private readonly UndoRedoManager<NoteState> _undoRedoManager = new();
        public ObservableCollection<MarkdownLineViewModel> Lines { get; set; }
        private int _nextDisplayOrder = 1;
        public int SubjectId { get; set; } = 1;
        public int CurrentCategoryId { get; set; } = 1;

        private Stack<(int categoryId, int level)> _categoryStack = new();


        private DispatcherTimer _idleTimer;
        private DateTime _lastActivityTime;
        private const int IDLE_TIMEOUT_SECONDS = 2; // 5초간 입력이 없으면 저장

        public NoteEditorViewModel()
        {
            Lines = new ObservableCollection<MarkdownLineViewModel>
            {
                new MarkdownLineViewModel
                {
                    IsEditing = true,  // 처음에 편집 가능하도록
                    SubjectId = SubjectId,
                    CategoryId = CurrentCategoryId,
                    Content = ""  // 빈 내용으로 시작
                }
            };

            InitializeIdleTimer();

            // PropertyChanged 이벤트 등록
            Lines[0].PropertyChanged += OnLinePropertyChanged;

            Lines.CollectionChanged += (s, e) =>
            {
                if (Lines.Count == 0)
                {
                    var newLine = new MarkdownLineViewModel
                    {
                        IsEditing = true,
                        SubjectId = SubjectId,
                        CategoryId = CurrentCategoryId,
                        Content = ""
                    };
                    newLine.PropertyChanged += OnLinePropertyChanged;
                    Lines.Add(newLine);
                }
            };
        }

        public NoteEditorViewModel(List<NoteCategory> loadedNotes)
        {
            Lines = new ObservableCollection<MarkdownLineViewModel>();
            InitializeIdleTimer();
            int currentDisplayOrder = 1;

            Debug.WriteLine($"[LOAD] NoteEditorViewModel 생성 시작. 카테고리 수: {loadedNotes?.Count ?? 0}");

            // 기본 카테고리 확인
            NoteRepository.EnsureDefaultCategory(SubjectId);

            if (loadedNotes != null && loadedNotes.Count > 0)
            {
                // 재귀적으로 카테고리와 라인 추가
                foreach (var category in loadedNotes)
                {
                    Debug.WriteLine($"[LOAD] 카테고리 처리: '{category.Title}' (ID: {category.CategoryId})");
                    currentDisplayOrder = AddCategoryWithHierarchy(category, currentDisplayOrder);
                }

                _nextDisplayOrder = currentDisplayOrder;
                Debug.WriteLine($"[LOAD] 로드 완료. 총 라인 수: {Lines.Count}");
            }

            // 라인이 없으면 빈 라인 추가
            if (Lines.Count == 0)
            {
                Debug.WriteLine("[LOAD] 로드된 데이터 없음. 빈 라인 추가.");
                AddInitialEmptyLine();
            }

            Lines.CollectionChanged += Lines_CollectionChanged;
        }

        private void AddInitialEmptyLine()
        {
            // 기본 카테고리 ID 사용 (1)
            var emptyLine = new MarkdownLineViewModel
            {
                IsEditing = true,
                SubjectId = SubjectId,
                CategoryId = 1, // 기본 카테고리
                Content = "",
                DisplayOrder = 1,
                TextId = 0,
                Index = 0
            };

            emptyLine.SetOriginalContent("");
            Lines.Add(emptyLine);
            RegisterLineEvents(emptyLine);

            CurrentCategoryId = 1;
            Debug.WriteLine($"[LOAD] 빈 라인 추가됨. CategoryId: {emptyLine.CategoryId}");
        }

        /// <summary>
        /// 카테고리와 하위 구조를 재귀적으로 추가
        /// </summary>
        private int AddCategoryWithHierarchy(NoteCategory category, int displayOrder)
        {
            CurrentCategoryId = category.CategoryId;

            Debug.WriteLine($"[LOAD] 카테고리 '{category.Title}' 추가 중...");

            // 카테고리 제목 추가
            var categoryLine = new MarkdownLineViewModel
            {
                Content = category.Title,
                IsEditing = false,
                SubjectId = SubjectId,
                CategoryId = category.CategoryId,
                TextId = 0,
                IsHeadingLine = true,
                Level = category.Level,
                DisplayOrder = displayOrder++
            };

            categoryLine.SetOriginalContent(category.Title);
            Lines.Add(categoryLine);
            RegisterLineEvents(categoryLine);

            Debug.WriteLine($"[LOAD] 카테고리 제목 라인 추가됨. 텍스트 수: {category.Lines.Count}");

            // 카테고리의 라인들 추가 (이미지 포함)
            foreach (var line in category.Lines)
            {
                var contentLine = new MarkdownLineViewModel
                {
                    Content = line.Content,
                    ContentType = line.ContentType ?? "text",
                    ImageUrl = line.ImageUrl,
                    IsEditing = false,
                    SubjectId = SubjectId,
                    CategoryId = category.CategoryId,
                    TextId = line.Index,
                    Index = Lines.Count,
                    DisplayOrder = displayOrder++
                };

                contentLine.SetOriginalContent(line.Content, line.ImageUrl);
                Lines.Add(contentLine);
                RegisterLineEvents(contentLine);

                if (line.ContentType == "image")
                {
                    Debug.WriteLine($"[LOAD] 이미지 라인 추가: URL={line.ImageUrl}");

                    // 이미지 파일 존재 확인
                    if (!string.IsNullOrEmpty(line.ImageUrl))
                    {
                        string fullPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, line.ImageUrl);
                        if (File.Exists(fullPath))
                        {
                            Debug.WriteLine($"[LOAD] 이미지 파일 확인됨: {fullPath}");
                        }
                        else
                        {
                            Debug.WriteLine($"[LOAD ERROR] 이미지 파일 없음: {fullPath}");
                        }
                    }
                }
                else
                {
                    Debug.WriteLine($"[LOAD] 텍스트 라인 추가: '{line.Content.Substring(0, Math.Min(30, line.Content.Length))}'...");
                }
            }

            // 하위 카테고리들 재귀적으로 추가
            foreach (var subCategory in category.SubCategories)
            {
                Debug.WriteLine($"[LOAD] 하위 카테고리 처리: '{subCategory.Title}'");
                displayOrder = AddCategoryWithHierarchy(subCategory, displayOrder);
            }

            return displayOrder;
        }

        private void InitializeIdleTimer()
        {
            _idleTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _idleTimer.Tick += CheckIdleAndSave;
            _idleTimer.Start();
            _lastActivityTime = DateTime.Now;
        }

        public void UpdateActivity()
        {
            Debug.WriteLine("마지막 액티비티 시간 바뀜");
            _lastActivityTime = DateTime.Now;
        }

        private void CheckIdleAndSave(object sender, EventArgs e)
        {
            if ((DateTime.Now - _lastActivityTime).TotalSeconds >= IDLE_TIMEOUT_SECONDS)
            {
                Debug.WriteLine($"[IDLE] {IDLE_TIMEOUT_SECONDS}초간 유휴 상태 감지. 자동 저장 시작.");
                DebugPrintCurrentState();
                UpdateActivity();
                SaveAllChanges();
            }
        }

        public class NoteState
        {
            public List<LineState> Lines { get; set; }
            public DateTime Timestamp { get; set; }
        }

        public class LineState
        {
            public string Content { get; set; }
            public int CategoryId { get; set; }
            public int TextId { get; set; }
            public bool IsHeadingLine { get; set; }
        }

        // 현재 상태 저장
        private void SaveCurrentState()
        {
            var state = new NoteState
            {
                Timestamp = DateTime.Now,
                Lines = Lines.Select(l => new LineState
                {
                    Content = l.Content,
                    CategoryId = l.CategoryId,
                    TextId = l.TextId,
                    IsHeadingLine = l.IsHeadingLine
                }).ToList()
            };

            _undoRedoManager.AddState(state);
        }

        // Ctrl+Z 처리
        public void Undo()
        {
            var previousState = _undoRedoManager.Undo();
            if (previousState != null)
            {
                RestoreState(previousState);
            }
        }

        // Ctrl+Y 처리
        public void Redo()
        {
            var nextState = _undoRedoManager.Redo();
            if (nextState != null)
            {
                RestoreState(nextState);
            }
        }

        private void RestoreState(NoteState state)
        {
            // 상태 복원 로직
            Lines.Clear();
            foreach (var lineState in state.Lines)
            {
                var line = new MarkdownLineViewModel
                {
                    Content = lineState.Content,
                    CategoryId = lineState.CategoryId,
                    TextId = lineState.TextId,
                    IsHeadingLine = lineState.IsHeadingLine,
                    SubjectId = SubjectId
                };
                Lines.Add(line);
                RegisterLineEvents(line);
            }
        }

        private void RegisterLineEvents(MarkdownLineViewModel line)
        {
            line.PropertyChanged += OnLinePropertyChanged;
            line.CategoryCreated += OnCategoryCreated;
            line.RequestFindPreviousCategory += OnRequestFindPreviousCategory;
        }

        private void UnregisterLineEvents(MarkdownLineViewModel line)
        {
            line.PropertyChanged -= OnLinePropertyChanged;
            line.CategoryCreated -= OnCategoryCreated;
            line.RequestFindPreviousCategory -= OnRequestFindPreviousCategory;
        }

        private void Lines_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            if (Lines.Count == 0)
            {
                var newLine = new MarkdownLineViewModel
                {
                    IsEditing = true,
                    SubjectId = SubjectId,
                    CategoryId = CurrentCategoryId,
                    Content = "",
                    DisplayOrder = 1
                };
                Lines.Add(newLine);
                RegisterLineEvents(newLine);
            }

            // 라인이 제거된 경우
            if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Remove)
            {
                foreach (MarkdownLineViewModel removedLine in e.OldItems)
                {
                    if (removedLine.TextId > 0)
                    {
                        NoteRepository.DeleteLine(removedLine.TextId);
                        Debug.WriteLine($"[DEBUG] 라인 삭제됨. TextId: {removedLine.TextId}");
                    }
                    UnregisterLineEvents(removedLine);
                }
            }

            // 인덱스 재정렬
            UpdateLineIndices();
        }



        /// <summary>
        /// 새로운 라인 추가
        /// </summary>
        public void AddNewLine()
        {
            int categoryIdForNewLine = GetCurrentCategoryIdForNewLine();
            int displayOrder = Lines.Count > 0 ? Lines.Last().DisplayOrder + 1 : 1;

            Debug.WriteLine($"[ADD LINE] 새 라인 추가 시작. CategoryId: {categoryIdForNewLine}, DisplayOrder: {displayOrder}");

            var newLine = new MarkdownLineViewModel
            {
                IsEditing = true,
                Content = "",
                SubjectId = SubjectId,
                CategoryId = categoryIdForNewLine > 0 ? categoryIdForNewLine : 1,
                Index = Lines.Count,
                DisplayOrder = displayOrder,
                TextId = 0
            };

            Lines.Add(newLine);
            RegisterLineEvents(newLine);

            Debug.WriteLine($"[ADD LINE] 새 라인 추가 완료. Index: {newLine.Index}");
        }

        private int GetCurrentCategoryIdForNewLine()
        {
            // 마지막 라인부터 역순으로 가장 최근의 카테고리 찾기
            for (int i = Lines.Count - 1; i >= 0; i--)
            {
                if (Lines[i].IsHeadingLine && Lines[i].CategoryId > 0)
                {
                    Debug.WriteLine($"[DEBUG] 가장 최근 카테고리 찾음: {Lines[i].CategoryId} (라인 {i}, 레벨 {Lines[i].Level})");
                    return Lines[i].CategoryId;
                }
            }

            Debug.WriteLine($"[DEBUG] 카테고리를 찾지 못함. CurrentCategoryId 사용: {CurrentCategoryId}");
            return CurrentCategoryId > 0 ? CurrentCategoryId : 1;
        }

        private void OnLinePropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (sender is MarkdownLineViewModel line)
            {
                if (e.PropertyName == nameof(MarkdownLineViewModel.Content))
                {
                    // 일반 텍스트가 제목으로 변경되는 경우
                    if (NoteRepository.IsCategoryHeading(line.Content) && !line.IsHeadingLine)
                    {
                        line.IsHeadingLine = true;
                        UpdateSubsequentLinesCategoryId(Lines.IndexOf(line) + 1, line.CategoryId);
                    }
                }
                else if (e.PropertyName == nameof(MarkdownLineViewModel.CategoryId))
                {
                    if (line.IsHeadingLine && line.CategoryId > 0)
                    {
                        UpdateCurrentCategory(line);
                    }
                }
            }
        }

        private void OnCategoryCreated(object sender, CategoryCreatedEventArgs e)
        {
            if (sender is MarkdownLineViewModel line)
            {
                CurrentCategoryId = e.NewCategoryId;
                Debug.WriteLine($"[DEBUG] 새 카테고리 생성됨. CurrentCategoryId 업데이트: {CurrentCategoryId}");

                // 이 제목 이후의 모든 라인들의 CategoryId 업데이트
                int headingIndex = Lines.IndexOf(line);
                UpdateSubsequentLinesCategoryId(headingIndex + 1, CurrentCategoryId);
            }
        }

        private void UpdateSubsequentLinesCategoryId(int startIndex, int categoryId)
        {
            for (int i = startIndex; i < Lines.Count; i++)
            {
                if (!Lines[i].IsHeadingLine)
                {
                    if (Lines[i].CategoryId != categoryId)
                    {
                        Lines[i].CategoryId = categoryId;
                        Debug.WriteLine($"[DEBUG] 라인 {i}의 CategoryId 업데이트: {categoryId}");
                    }
                }
                else
                {
                    break; // 다음 제목을 만나면 중단
                }
            }
        }


        private void OnRequestFindPreviousCategory(object sender, FindPreviousCategoryEventArgs e)
        {
            var currentLine = e.CurrentLine;
            int currentIndex = Lines.IndexOf(currentLine);

            // 현재 라인 이전에서 가장 가까운 카테고리 찾기
            for (int i = currentIndex - 1; i >= 0; i--)
            {
                if (Lines[i].IsHeadingLine && Lines[i].CategoryId > 0)
                {
                    e.PreviousCategoryId = Lines[i].CategoryId;
                    return;
                }
            }

            // 이전 카테고리가 없으면 기본값
            e.PreviousCategoryId = 1;
        }

        private void UpdateCurrentCategory(MarkdownLineViewModel headingLine)
        {
            // 제목 라인이 저장된 후 CategoryId가 설정되면 현재 카테고리로 설정
            if (headingLine.CategoryId > 0)
            {
                CurrentCategoryId = headingLine.CategoryId;
                Debug.WriteLine($"[DEBUG] 현재 카테고리 변경됨: {CurrentCategoryId}");

                // 이 제목 이후의 모든 라인들의 CategoryId 업데이트
                int headingIndex = Lines.IndexOf(headingLine);
                for (int i = headingIndex + 1; i < Lines.Count; i++)
                {
                    if (!Lines[i].IsHeadingLine) // 다음 제목이 나올 때까지
                    {
                        Lines[i].CategoryId = CurrentCategoryId;
                    }
                    else
                    {
                        break; // 다음 제목을 만나면 중단
                    }
                }
            }
        }

        public void RemoveLine(MarkdownLineViewModel line)
        {
            if (Lines.Contains(line))
            {
                if (line.IsHeadingLine && line.CategoryId > 0)
                {
                    // 하위 텍스트들을 이전 카테고리로 재할당
                    int previousCategoryId = GetPreviousCategoryId(Lines.IndexOf(line));
                    if (previousCategoryId > 0)
                    {
                        NoteRepository.ReassignTextsToCategory(line.CategoryId, previousCategoryId);
                    }

                    // 카테고리만 삭제 (텍스트는 재할당됨)
                    NoteRepository.DeleteCategory(line.CategoryId, false);

                    // 현재 카테고리가 삭제되는 경우
                    if (CurrentCategoryId == line.CategoryId)
                    {
                        CurrentCategoryId = previousCategoryId > 0 ? previousCategoryId : 1;
                    }
                }

                Lines.Remove(line);
                UnregisterLineEvents(line); // 이벤트 해제
            }
        }

        private int GetPreviousCategoryId(int currentIndex)
        {
            for (int i = currentIndex - 1; i >= 0; i--)
            {
                if (Lines[i].IsHeadingLine && Lines[i].CategoryId > 0)
                {
                    return Lines[i].CategoryId;
                }
            }
            return 1; // 기본 카테고리
        }

        private void UpdateCurrentCategoryAfterDeletion()
        {
            // 가장 마지막 제목의 CategoryId를 현재 카테고리로 설정
            var lastHeading = Lines.LastOrDefault(l => l.IsHeadingLine && l.CategoryId > 0);
            if (lastHeading != null)
            {
                CurrentCategoryId = lastHeading.CategoryId;
            }
            else
            {
                // 제목이 없으면 기본 카테고리 사용
                CurrentCategoryId = 1;
            }

            Debug.WriteLine($"[DEBUG] 삭제 후 현재 카테고리: {CurrentCategoryId}");
        }

        public void InsertNewLineAt(int index)
        {
            if (index < 0 || index > Lines.Count)
                index = Lines.Count;

            // 삽입 위치에서의 CategoryId 결정
            int categoryId = DetermineCategoryIdForIndex(index);
            int displayOrder = GetDisplayOrderForIndex(index);

            Debug.WriteLine($"[INSERT] 새 라인 삽입. Index: {index}, CategoryId: {categoryId}, DisplayOrder: {displayOrder}");

            var newLine = new MarkdownLineViewModel
            {
                IsEditing = true,
                Content = "",
                SubjectId = SubjectId,
                CategoryId = categoryId,
                Index = index,
                DisplayOrder = displayOrder,
                TextId = 0
            };

            // UI에 즉시 반영
            Lines.Insert(index, newLine);

            // 이후 라인들의 Index 업데이트
            for (int i = index + 1; i < Lines.Count; i++)
            {
                Lines[i].Index = i;
                if (Lines[i].DisplayOrder <= displayOrder)
                {
                    Lines[i].DisplayOrder = displayOrder + (i - index);
                }
            }

            // 이벤트 등록
            RegisterLineEvents(newLine);

            Debug.WriteLine($"[INSERT] 새 라인 삽입 완료");
        }

        private int DetermineCategoryIdForIndex(int index)
        {
            // 인덱스 이전의 가장 가까운 카테고리 찾기
            for (int i = index - 1; i >= 0; i--)
            {
                if (Lines[i].IsHeadingLine && Lines[i].CategoryId > 0)
                {
                    return Lines[i].CategoryId;
                }
                else if (!Lines[i].IsHeadingLine && Lines[i].CategoryId > 0)
                {
                    return Lines[i].CategoryId;
                }
            }

            // 찾을 수 없으면 이후 라인에서 찾기
            for (int i = index; i < Lines.Count; i++)
            {
                if (Lines[i].CategoryId > 0)
                {
                    return Lines[i].CategoryId;
                }
            }

            // 그래도 없으면 기본 카테고리
            return 1;
        }


        public void InsertNewLineAfter(MarkdownLineViewModel afterLine)
        {
            int insertIndex = Lines.IndexOf(afterLine) + 1;
            int insertDisplayOrder = afterLine.DisplayOrder;

            // 이후 라인들의 displayOrder 증가
            ShiftDisplayOrdersFrom(insertDisplayOrder + 1);

            // 새 라인 생성
            var newLine = new MarkdownLineViewModel
            {
                IsEditing = true,
                Content = "",
                SubjectId = SubjectId,
                CategoryId = afterLine.CategoryId,
                Index = insertIndex,
                DisplayOrder = insertDisplayOrder + 1,
                TextId = 0
            };

            Lines.Insert(insertIndex, newLine);
            newLine.PropertyChanged += OnLinePropertyChanged;
            newLine.CategoryCreated += OnCategoryCreated;

            Debug.WriteLine($"[DEBUG] 새 라인 삽입. Index: {insertIndex}, DisplayOrder: {newLine.DisplayOrder}");
        }

        private void ShiftDisplayOrdersFrom(int fromOrder)
        {
            // 메모리에서 먼저 업데이트
            var linesToShift = Lines.Where(l => l.DisplayOrder >= fromOrder).ToList();
            foreach (var line in linesToShift)
            {
                line.DisplayOrder++;
                Debug.WriteLine($"[DEBUG] 라인 시프트: Content='{line.Content}', NewOrder={line.DisplayOrder}");
            }

            // DB에서도 업데이트
            NoteRepository.ShiftDisplayOrdersAfter(SubjectId, fromOrder - 1);
        }



        /// <summary>
        /// 모든 라인의 인덱스를 재정렬
        /// </summary>
        private void UpdateLineIndices()
        {
            for (int i = 0; i < Lines.Count; i++)
            {
                Lines[i].Index = i;
            }
        }

        public void UpdateAllCategoryIds()
        {
            int currentCategoryId = 1; // 기본 카테고리

            foreach (var line in Lines)
            {
                if (line.IsHeadingLine && line.CategoryId > 0)
                {
                    currentCategoryId = line.CategoryId;
                }
                else if (!line.IsHeadingLine)
                {
                    line.CategoryId = currentCategoryId;
                }
            }

            CurrentCategoryId = currentCategoryId;
            Debug.WriteLine($"[DEBUG] 모든 라인의 CategoryId 업데이트 완료. 현재 카테고리: {CurrentCategoryId}");
        }

        // 변경된 라인만 저장
        public void SaveAllChanges()
        {
            try
            {
                // DisplayOrder가 변경된 라인도 포함
                var changedLines = Lines.Where(l =>
                    l.HasChanges ||
                    l.DisplayOrder != l.Index + 1 ||
                    l.TextId > 0 && !l.IsHeadingLine // 기존 텍스트도 확인
                ).ToList();

                if (!changedLines.Any())
                {
                    Debug.WriteLine("[SAVE] 변경사항 없음");
                    return;
                }

                Debug.WriteLine($"[SAVE] {changedLines.Count}개 라인 저장 시작");
                DebugPrintCurrentState();

                using var transaction = NoteRepository.BeginTransaction();
                try
                {
                    // 먼저 모든 DisplayOrder 업데이트
                    UpdateAllDisplayOrders(transaction);

                    foreach (var line in changedLines)
                    {
                        SaveLine(line, transaction);
                    }

                    transaction.Commit();
                    Debug.WriteLine($"[SAVE] 트랜잭션 커밋 완료");

                    // 저장 후 원본 상태 업데이트
                    foreach (var line in changedLines)
                    {
                        line.ResetChanges();
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[SAVE ERROR] 트랜잭션 실패, 롤백: {ex.Message}");
                    transaction.Rollback();
                    throw;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SAVE ERROR] 저장 실패: {ex.Message}\n{ex.StackTrace}");
            }
        }

        private void UpdateAllDisplayOrders(NoteRepository.Transaction transaction)
        {
            foreach (var line in Lines)
            {
                if (line.IsHeadingLine && line.CategoryId > 0)
                {
                    NoteRepository.UpdateCategoryDisplayOrder(line.CategoryId, line.DisplayOrder, transaction);
                }
                else if (!line.IsHeadingLine && line.TextId > 0)
                {
                    NoteRepository.UpdateLineDisplayOrder(line.TextId, line.DisplayOrder, transaction);
                }
            }
        }

        

        private void SaveLine(MarkdownLineViewModel line, NoteRepository.Transaction transaction)
        {
            Debug.WriteLine($"[SAVE] 라인 처리 - Content: {line.Content?.Substring(0, Math.Min(30, line.Content?.Length ?? 0))}, " +
                           $"IsHeading: {line.IsHeadingLine}, IsImage: {line.IsImage}, " +
                           $"CategoryId: {line.CategoryId}, TextId: {line.TextId}, " +
                           $"DisplayOrder: {line.DisplayOrder}");

            if (line.IsHeadingLine)
            {
                SaveHeading(line, transaction);
            }
            else
            {
                SaveContent(line, transaction);
            }
        }

        private void SaveHeading(MarkdownLineViewModel line, NoteRepository.Transaction transaction)
        {
            int? parentId = FindParentForHeading(line);

            if (line.CategoryId <= 0)
            {
                // 새 카테고리 생성
                int newCategoryId = NoteRepository.InsertCategory(
                    line.Content,
                    line.SubjectId,
                    line.DisplayOrder,
                    line.Level,
                    parentId,
                    transaction);
                line.CategoryId = newCategoryId;
                Debug.WriteLine($"[SAVE] 새 카테고리 생성됨: {newCategoryId}");
            }
            else
            {
                // 기존 카테고리 업데이트
                NoteRepository.UpdateCategory(line.CategoryId, line.Content, transaction);
                NoteRepository.UpdateCategoryDisplayOrder(line.CategoryId, line.DisplayOrder, transaction);
                Debug.WriteLine($"[SAVE] 카테고리 업데이트됨: {line.CategoryId}");
            }
        }

        private void SaveContent(MarkdownLineViewModel line, NoteRepository.Transaction transaction)
        {
            if (line.CategoryId <= 0)
            {
                Debug.WriteLine($"[SAVE ERROR] CategoryId가 유효하지 않음: {line.CategoryId}");
                line.CategoryId = GetCurrentCategoryIdForNewLine();
                Debug.WriteLine($"[SAVE] CategoryId 재설정: {line.CategoryId}");
            }

            if (line.TextId <= 0)
            {
                // 새 텍스트 생성
                int newTextId = NoteRepository.InsertNewLine(
                    line.Content,
                    line.SubjectId,
                    line.CategoryId,
                    line.DisplayOrder,
                    line.ContentType,
                    line.ImageUrl,
                    transaction);

                if (newTextId > 0)
                {
                    line.TextId = newTextId;
                    Debug.WriteLine($"[SAVE] 새 {line.ContentType} 생성됨: {newTextId}");
                }
            }
            else
            {
                // 기존 텍스트 업데이트
                NoteRepository.UpdateLine(line, transaction);
                NoteRepository.UpdateLineDisplayOrder(line.TextId, line.DisplayOrder, transaction);
                Debug.WriteLine($"[SAVE] {line.ContentType} 업데이트됨: {line.TextId}");
            }
        }


        /// <summary>
        /// 헤딩의 부모 카테고리 찾기
        /// </summary>
        private int? FindParentForHeading(MarkdownLineViewModel heading)
        {

            Debug.WriteLine($"[SAVE] 부모 찾는다 기다려라");

            int headingIndex = Lines.IndexOf(heading);

            for (int i = headingIndex - 1; i >= 0; i--)
            {
                Debug.WriteLine($"[SAVE] 부모 찾는 중이다 기다려라");

                var line = Lines[i];
                if (line.IsHeadingLine && line.Level < heading.Level && line.CategoryId > 0)
                {
                    Debug.WriteLine($"[SAVE] 부모 찾았다 임마 기다려라");

                    return line.CategoryId;
                }
            }

            Debug.WriteLine($"[SAVE] 부모 몬 찾았다 어어?");


            return null;
        }

        public void InsertImageLineAt(int index, string imagePath)
        {
            if (index < 0 || index > Lines.Count)
                index = Lines.Count;

            int categoryId = GetCurrentCategoryIdForNewLine();
            int displayOrder = GetDisplayOrderForIndex(index);

            var imageLine = new MarkdownLineViewModel
            {
                IsEditing = false,
                Content = $"![이미지]({imagePath})", // 마크다운 이미지 문법
                ImageUrl = imagePath,
                ContentType = "image",
                SubjectId = SubjectId,
                CategoryId = categoryId,
                Index = index,
                DisplayOrder = displayOrder,
                TextId = 0
            };

            Lines.Insert(index, imageLine);
            RegisterLineEvents(imageLine);

            // 이후 라인들의 Index와 DisplayOrder 업데이트
            UpdateLineIndicesFrom(index + 1);

            Debug.WriteLine($"[IMAGE] 이미지 라인 추가됨. Index: {index}, Path: {imagePath}");
        }

        private int GetDisplayOrderForIndex(int index)
        {
            if (index == 0)
                return 1;

            if (index >= Lines.Count)
                return Lines.Count > 0 ? Lines.Last().DisplayOrder + 1 : 1;

            // 이전 라인과 다음 라인 사이의 값
            int prevOrder = Lines[index - 1].DisplayOrder;
            int nextOrder = Lines[index].DisplayOrder;

            if (nextOrder - prevOrder > 1)
                return prevOrder + 1;

            // 공간이 없으면 이후 모든 라인들의 DisplayOrder를 밀어냄
            ShiftDisplayOrdersFrom(nextOrder);
            return nextOrder;
        }

        private void UpdateLineIndicesFrom(int startIndex)
        {
            for (int i = startIndex; i < Lines.Count; i++)
            {
                Lines[i].Index = i;
            }
        }


        public void ReorderLine(MarkdownLineViewModel draggedLine, MarkdownLineViewModel targetLine, bool insertBefore)
        {
            if (draggedLine == null || targetLine == null || draggedLine == targetLine)
                return;

            int draggedIndex = Lines.IndexOf(draggedLine);
            int targetIndex = Lines.IndexOf(targetLine);

            if (draggedIndex < 0 || targetIndex < 0)
                return;

            Debug.WriteLine($"[DRAG] 시작 - Dragged: {draggedIndex}, Target: {targetIndex}, InsertBefore: {insertBefore}");

            // 라인 제거
            Lines.RemoveAt(draggedIndex);

            // 새 위치 계산
            int newIndex = targetIndex;
            if (draggedIndex < targetIndex)
            {
                newIndex = insertBefore ? targetIndex - 1 : targetIndex;
            }
            else
            {
                newIndex = insertBefore ? targetIndex : targetIndex + 1;
            }

            // 라인 삽입
            Lines.Insert(newIndex, draggedLine);

            // 모든 라인의 DisplayOrder와 Index 재정렬
            ReorderAllLines();

            // 카테고리 재할당
            ReassignCategories();

            Debug.WriteLine($"[DRAG] 완료 - 이동: {draggedIndex} -> {newIndex}");
        }

        private void ReorderAllLines()
        {
            for (int i = 0; i < Lines.Count; i++)
            {
                var line = Lines[i];
                line.Index = i;
                line.DisplayOrder = i + 1;

                // 변경사항 표시
                if (!line.HasChanges)
                {
                    line.OnPropertyChanged(nameof(line.DisplayOrder));
                }

                Debug.WriteLine($"[REORDER] Index: {i}, DisplayOrder: {line.DisplayOrder}, Content: {line.Content?.Substring(0, Math.Min(20, line.Content?.Length ?? 0))}");
            }
        }

        private void ReassignCategories()
        {
            int currentCategoryId = 1; // 기본 카테고리

            for (int i = 0; i < Lines.Count; i++)
            {
                var line = Lines[i];

                if (line.IsHeadingLine)
                {
                    // 헤딩이면 현재 카테고리 업데이트
                    if (line.CategoryId > 0)
                    {
                        currentCategoryId = line.CategoryId;
                    }
                }
                else
                {
                    // 일반 텍스트나 이미지는 현재 카테고리에 할당
                    if (line.CategoryId != currentCategoryId)
                    {
                        Debug.WriteLine($"[REASSIGN] 라인 {i}의 CategoryId 변경: {line.CategoryId} -> {currentCategoryId}");
                        line.CategoryId = currentCategoryId;

                        // 변경사항 표시
                        if (!line.HasChanges)
                        {
                            line.OnPropertyChanged(nameof(line.CategoryId));
                        }
                    }
                }
            }
        }

        private void ReorderDisplayOrders()
        {
            for (int i = 0; i < Lines.Count; i++)
            {
                Lines[i].Index = i;
                Lines[i].DisplayOrder = i + 1;
            }

            // DisplayOrder 변경을 데이터베이스에 반영하기 위해 모든 라인을 변경됨으로 표시
            foreach (var line in Lines)
            {
                line.OnPropertyChanged(nameof(line.DisplayOrder));
            }
        }

        public void ForceFullSave()
        {
            try
            {
                Debug.WriteLine("[SAVE] 프로그램 종료 - 전체 저장 시작");

                // 모든 라인 저장 (변경사항 여부와 관계없이)
                using var transaction = NoteRepository.BeginTransaction();
                try
                {
                    // 모든 DisplayOrder 업데이트
                    UpdateAllDisplayOrders(transaction);

                    // 모든 라인 저장
                    foreach (var line in Lines)
                    {
                        if (line.IsHeadingLine && line.CategoryId > 0)
                        {
                            NoteRepository.UpdateCategory(line.CategoryId, line.Content, transaction);
                        }
                        else if (!line.IsHeadingLine)
                        {
                            if (line.TextId <= 0)
                            {
                                // 새 라인
                                SaveContent(line, transaction);
                            }
                            else
                            {
                                // 기존 라인
                                NoteRepository.UpdateLine(line, transaction);
                            }
                        }
                    }

                    transaction.Commit();
                    Debug.WriteLine("[SAVE] 프로그램 종료 - 전체 저장 완료");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[SAVE ERROR] 프로그램 종료 저장 실패: {ex.Message}");
                    transaction.Rollback();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SAVE ERROR] ForceFullSave 실패: {ex.Message}");
            }
        }

        private void DebugPrintCurrentState()
        {
            Debug.WriteLine("=== 현재 에디터 상태 ===");
            Debug.WriteLine($"SubjectId: {SubjectId}");
            Debug.WriteLine($"CurrentCategoryId: {CurrentCategoryId}");
            Debug.WriteLine($"Lines 개수: {Lines.Count}");

            for (int i = 0; i < Lines.Count; i++)
            {
                var line = Lines[i];
                Debug.WriteLine($"[{i}] " +
                               $"Type: {(line.IsHeadingLine ? "HEADING" : line.IsImage ? "IMAGE" : "TEXT")}, " +
                               $"Content: '{line.Content?.Substring(0, Math.Min(30, line.Content?.Length ?? 0))}', " +
                               $"CategoryId: {line.CategoryId}, " +
                               $"TextId: {line.TextId}, " +
                               $"DisplayOrder: {line.DisplayOrder}, " +
                               $"HasChanges: {line.HasChanges}");

                if (line.IsImage)
                {
                    Debug.WriteLine($"     ImageUrl: {line.ImageUrl}");
                }
            }
            Debug.WriteLine("===================");
        }

        // 데이터 무결성 검증
        public void ValidateDataIntegrity()
        {
            Debug.WriteLine("=== 데이터 무결성 검증 ===");

            // 1. DisplayOrder 중복 검사
            var duplicateOrders = Lines.GroupBy(l => l.DisplayOrder)
                                       .Where(g => g.Count() > 1)
                                       .Select(g => g.Key);

            if (duplicateOrders.Any())
            {
                Debug.WriteLine($"[ERROR] DisplayOrder 중복 발견: {string.Join(", ", duplicateOrders)}");
            }

            // 2. CategoryId 검증
            int orphanedLines = 0;
            foreach (var line in Lines.Where(l => !l.IsHeadingLine))
            {
                if (line.CategoryId <= 0)
                {
                    Debug.WriteLine($"[ERROR] CategoryId가 없는 라인: Index={line.Index}, Content={line.Content?.Substring(0, 20)}");
                    orphanedLines++;
                }
            }

            if (orphanedLines > 0)
            {
                Debug.WriteLine($"[ERROR] 고아 라인 개수: {orphanedLines}");
            }

            // 3. 연속성 검증
            for (int i = 0; i < Lines.Count; i++)
            {
                if (Lines[i].Index != i)
                {
                    Debug.WriteLine($"[ERROR] Index 불일치: 실제={i}, 저장됨={Lines[i].Index}");
                }
            }

            Debug.WriteLine("===================");
        }

        // View가 닫힐 때 호출
        public void OnViewClosing()
        {
            _idleTimer?.Stop();
            ForceFullSave(); // SaveAllChanges 대신 ForceFullSave 호출
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string property = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(property));
        }
    }
}