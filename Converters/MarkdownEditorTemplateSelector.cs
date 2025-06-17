﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows;
using SP.Modules.Notes.ViewModels;

namespace SP.Converters
{
    public class MarkdownEditorTemplateSelector : DataTemplateSelector
    {
        public DataTemplate EditTemplate { get; set; }
        public DataTemplate ViewTemplate { get; set; }

        public override DataTemplate SelectTemplate(object item, DependencyObject container)
        {
            var line = item as MarkdownLineViewModel;
            return line?.IsEditing == true ? EditTemplate : ViewTemplate;
        }
    }
}
