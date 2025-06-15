using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using SP.Modules.Daily.ViewModels;
using SP.Modules.Subjects.ViewModels;

namespace SP.Modules.Common.Views
{
    public partial class SubjectProgressListView : UserControl
    {
        private Point _startPoint;
        private bool _isDragging = false;

        public SubjectProgressListView()
        {
            InitializeComponent();
        }

        public ObservableCollection<SubjectProgressViewModel> Subjects
        {
            get => (ObservableCollection<SubjectProgressViewModel>)GetValue(SubjectsProperty);
            set => SetValue(SubjectsProperty, value);
        }

        public static readonly DependencyProperty SubjectsProperty =
            DependencyProperty.Register("Subjects", typeof(ObservableCollection<SubjectProgressViewModel>), typeof(SubjectProgressListView), new PropertyMetadata(null));

        // 중앙 패널에서 좌측으로 드래그하기 위한 이벤트
        private void SubjectName_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _startPoint = e.GetPosition(null);
            _isDragging = false;
        }

        private void SubjectName_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed && !_isDragging)
            {
                Point mousePos = e.GetPosition(null);
                Vector diff = _startPoint - mousePos;

                if (System.Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
                    System.Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance)
                {
                    _isDragging = true;

                    var textBlock = sender as TextBlock;
                    var subject = textBlock?.DataContext as SubjectProgressViewModel;

                    if (subject != null)
                    {
                        var dragData = new DataObject("RemoveSubjectData", subject);
                        DragDrop.DoDragDrop(textBlock, dragData, DragDropEffects.Move);
                    }

                    _isDragging = false;
                }
            }
        }

        private void SubjectProgressListView_DragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent("SubjectData"))
            {
                e.Effects = DragDropEffects.Copy;
                DropTargetBorder.Tag = "DragOver";
            }
            else
            {
                e.Effects = DragDropEffects.None;
                DropTargetBorder.Tag = null;
            }
            e.Handled = true;
        }

        private void SubjectProgressListView_Drop(object sender, DragEventArgs e)
        {
            DropTargetBorder.Tag = null;
            System.Diagnostics.Debug.WriteLine("[DragDrop] Drop 이벤트 발생");

            try
            {
                if (e.Data.GetDataPresent("SubjectData"))
                {
                    var droppedSubject = e.Data.GetData("SubjectData") as SubjectGroupViewModel;
                    System.Diagnostics.Debug.WriteLine($"[DragDrop] 드롭된 과목: {droppedSubject?.SubjectName}");

                    if (droppedSubject != null && DataContext is DailyBodyViewModel dailyBodyVM)
                    {
                        var newSubjectProgress = new SubjectProgressViewModel
                        {
                            SubjectName = droppedSubject.SubjectName,
                            Progress = 0.0,
                            StudyTimeMinutes = 0
                        };

                        // 안전한 추가 메소드 사용
                        dailyBodyVM.AddSubjectSafely(newSubjectProgress);
                    }
                }
                else if (e.Data.GetDataPresent("TopicData"))
                {
                    var droppedTopic = e.Data.GetData("TopicData") as TopicGroupViewModel;
                    System.Diagnostics.Debug.WriteLine($"[DragDrop] 드롭된 분류: {droppedTopic?.GroupTitle} (부모: {droppedTopic?.ParentSubjectName})");

                    if (droppedTopic != null && !string.IsNullOrEmpty(droppedTopic.ParentSubjectName) &&
                        DataContext is DailyBodyViewModel dailyBodyVM)
                    {
                        // 부모 과목이 이미 있는지 확인
                        var existingSubject = dailyBodyVM.Subjects.FirstOrDefault(s =>
                            string.Equals(s.SubjectName, droppedTopic.ParentSubjectName, StringComparison.OrdinalIgnoreCase));

                        if (existingSubject != null)
                        {
                            // 기존 과목에 분류 추가
                            var existingTopic = existingSubject.TopicGroups.FirstOrDefault(t =>
                                string.Equals(t.GroupTitle, droppedTopic.GroupTitle, StringComparison.OrdinalIgnoreCase));

                            if (existingTopic == null)
                            {
                                existingSubject.TopicGroups.Add(droppedTopic);
                                System.Diagnostics.Debug.WriteLine($"[DragDrop] 기존 과목 '{droppedTopic.ParentSubjectName}'에 분류 '{droppedTopic.GroupTitle}' 추가됨");
                            }
                        }
                        else
                        {
                            // 새 과목과 분류 함께 추가
                            var newSubjectProgress = new SubjectProgressViewModel
                            {
                                SubjectName = droppedTopic.ParentSubjectName,
                                Progress = 0.0,
                                StudyTimeMinutes = 0
                            };
                            newSubjectProgress.TopicGroups.Add(droppedTopic);

                            dailyBodyVM.AddSubjectSafely(newSubjectProgress);
                            System.Diagnostics.Debug.WriteLine($"[DragDrop] 분류와 함께 과목 '{droppedTopic.ParentSubjectName}' 추가됨");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DragDrop] 오류 발생: {ex.Message}");
            }

            e.Handled = true;
        }
    }
}