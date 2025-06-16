﻿using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using SP.Modules.Common.Helpers;
using SP.Modules.Daily.ViewModels;
using SP.Modules.Subjects.ViewModels;
using SP.ViewModels;

namespace SP.Modules.Common.Views
{
    public partial class SubjectListView : UserControl
    {
        private Point _startPoint;
        private bool _isDragging = false;

        public SubjectListView()
        {
            InitializeComponent();
        }

        // 과목 드래그 이벤트
        private void SubjectGrid_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _startPoint = e.GetPosition(null);
            _isDragging = false;
        }

        private void SubjectGrid_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed && !_isDragging)
            {
                Point mousePos = e.GetPosition(null);
                Vector diff = _startPoint - mousePos;

                if (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
                    Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance)
                {
                    _isDragging = true;

                    var grid = sender as Grid;
                    var subject = grid?.DataContext as SubjectGroupViewModel;

                    if (subject != null)
                    {
                        var dragData = new DataObject("SubjectData", subject);
                        DragDrop.DoDragDrop(grid, dragData, DragDropEffects.Copy);
                        System.Diagnostics.Debug.WriteLine($"[DragDrop] 과목 '{subject.SubjectName}' 드래그 시작");
                    }

                    _isDragging = false;
                }
            }
        }

        // 분류(TopicGroup) 드래그 이벤트 - 개선된 버전
        private void TopicGroup_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _startPoint = e.GetPosition(null);
            _isDragging = false;
        }

        private void TopicGroup_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed && !_isDragging)
            {
                Point mousePos = e.GetPosition(null);
                Vector diff = _startPoint - mousePos;

                if (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
                    Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance)
                {
                    _isDragging = true;

                    var grid = sender as Grid;
                    var topicGroup = grid?.DataContext as TopicGroupViewModel;

                    if (topicGroup != null)
                    {
                        // 부모 과목명 찾기 - 개선된 방법
                        var parentSubject = FindParentSubject(grid);
                        if (parentSubject != null)
                        {
                            // TopicGroupViewModel에 부모 정보 설정
                            topicGroup.ParentSubjectName = parentSubject.SubjectName;

                            // 드래그 데이터에 부모 정보도 함께 전달
                            var dragData = new DataObject();
                            dragData.SetData("TopicData", topicGroup);
                            dragData.SetData("ParentSubjectName", parentSubject.SubjectName);

                            DragDrop.DoDragDrop(grid, dragData, DragDropEffects.Copy);
                            System.Diagnostics.Debug.WriteLine($"[DragDrop] 분류 '{topicGroup.GroupTitle}' 드래그 시작 (부모: {topicGroup.ParentSubjectName})");
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine($"[DragDrop] 분류 '{topicGroup.GroupTitle}'의 부모 과목을 찾을 수 없음");
                        }
                    }

                    _isDragging = false;
                }
            }
        }

        // 부모 과목 찾기 헬퍼 메소드 - 개선된 버전
        private SubjectGroupViewModel FindParentSubject(DependencyObject child)
        {
            DependencyObject parent = System.Windows.Media.VisualTreeHelper.GetParent(child);

            while (parent != null)
            {
                if (parent is FrameworkElement element)
                {
                    // SubjectGroupViewModel 타입의 DataContext를 찾을 때까지 올라감
                    if (element.DataContext is SubjectGroupViewModel subject)
                    {
                        System.Diagnostics.Debug.WriteLine($"[DragDrop] 부모 과목 찾음: {subject.SubjectName}");
                        return subject;
                    }
                }
                parent = System.Windows.Media.VisualTreeHelper.GetParent(parent);
            }

            System.Diagnostics.Debug.WriteLine("[DragDrop] 부모 과목을 찾을 수 없음");
            return null;
        }

        private void SubjectList_DragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent("RemoveSubjectData") ||
                e.Data.GetDataPresent("RemoveTopicGroupData"))
            {
                e.Effects = DragDropEffects.Move;
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
            e.Handled = true;
        }

        private void SubjectList_Drop(object sender, DragEventArgs e)
        {
            // 과목 전체 제거
            if (e.Data.GetDataPresent("RemoveSubjectData"))
            {
                var subjectToRemove = e.Data.GetData("RemoveSubjectData") as SubjectProgressViewModel;
                if (subjectToRemove != null)
                {
                    // 공유 데이터에서 제거
                    if (Window.GetWindow(this)?.DataContext is MainViewModel mainVM)
                    {
                        var existingSubject = mainVM.SharedSubjectProgress.FirstOrDefault(s =>
                            string.Equals(s.SubjectName, subjectToRemove.SubjectName, StringComparison.OrdinalIgnoreCase));

                        if (existingSubject != null)
                        {
                            mainVM.SharedSubjectProgress.Remove(existingSubject);
                            System.Diagnostics.Debug.WriteLine($"[DragDrop] 과목 '{subjectToRemove.SubjectName}' 전체 제거됨 (모든 하위 TopicGroup 포함)");

                            var dbHelper = DatabaseHelper.Instance;
                            dbHelper.RemoveDailySubject(DateTime.Today, subjectToRemove.SubjectName);
                        }
                    }
                }
            }
            // TopicGroup 단독 제거 (부모 과목은 유지)
            else if (e.Data.GetDataPresent("RemoveTopicGroupData"))
            {
                var topicGroupToRemove = e.Data.GetData("RemoveTopicGroupData") as TopicGroupViewModel;
                if (topicGroupToRemove != null && Window.GetWindow(this)?.DataContext is MainViewModel mainVM)
                {
                    var parentSubject = mainVM.SharedSubjectProgress.FirstOrDefault(s =>
                        string.Equals(s.SubjectName, topicGroupToRemove.ParentSubjectName, StringComparison.OrdinalIgnoreCase));

                    if (parentSubject != null)
                    {
                        var existingTopicGroup = parentSubject.TopicGroups.FirstOrDefault(t =>
                            string.Equals(t.GroupTitle, topicGroupToRemove.GroupTitle, StringComparison.OrdinalIgnoreCase));

                        if (existingTopicGroup != null)
                        {
                            parentSubject.TopicGroups.Remove(existingTopicGroup);
                            System.Diagnostics.Debug.WriteLine($"[DragDrop] TopicGroup '{topicGroupToRemove.GroupTitle}' 제거됨");
                            System.Diagnostics.Debug.WriteLine($"[DragDrop] 부모 과목 '{parentSubject.SubjectName}' 유지됨 (TopicGroup 개수: {parentSubject.TopicGroups.Count})");
                        }
                    }
                }
            }
            e.Handled = true;
        }
    }
}