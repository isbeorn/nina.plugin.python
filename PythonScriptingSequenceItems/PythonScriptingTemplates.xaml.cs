using ICSharpCode.AvalonEdit;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Runtime.Versioning;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;

namespace NINA.Plugin.Python.PythonScriptingTestCategory {

    [Export(typeof(ResourceDictionary))]
    public partial class PluginItemTemplate : ResourceDictionary {

        public PluginItemTemplate() {
            InitializeComponent();
        }
    }

    [SupportedOSPlatform("windows7.0")]
    public static class TextEditorHelper {

        public static readonly DependencyProperty BindableTextProperty =
            DependencyProperty.RegisterAttached(
                "BindableText",
                typeof(string),
                typeof(TextEditorHelper),
                new FrameworkPropertyMetadata(default(string), FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnBindableTextChanged)
            );

        public static string GetBindableText(DependencyObject obj)
            => (string)obj.GetValue(BindableTextProperty);

        public static void SetBindableText(DependencyObject obj, string value)
            => obj.SetValue(BindableTextProperty, value);

        public static readonly DependencyProperty UseApplicationThemeProperty =
            DependencyProperty.RegisterAttached(
                "UseApplicationTheme",
                typeof(bool),
                typeof(TextEditorHelper),
                new PropertyMetadata(false, OnUseApplicationThemeChanged)
            );

        public static bool GetUseApplicationTheme(DependencyObject obj)
            => (bool)obj.GetValue(UseApplicationThemeProperty);

        public static void SetUseApplicationTheme(DependencyObject obj, bool value)
            => obj.SetValue(UseApplicationThemeProperty, value);

        private static void OnBindableTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
            if (d is not TextEditor editor)
                return;

            editor.TextChanged -= EditorOnTextChanged;

            var newText = e.NewValue as string ?? "";
            if (editor.Text != newText)
                editor.Text = newText;

            editor.TextChanged += EditorOnTextChanged;
        }

        private static void OnUseApplicationThemeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
            if (d is not TextEditor editor)
                return;

            editor.Loaded -= EditorOnLoaded;

            if (e.NewValue is true) {
                ApplyApplicationTheme(editor);
                editor.Loaded += EditorOnLoaded;
            }
        }

        private static void EditorOnLoaded(object sender, RoutedEventArgs e) {
            if (sender is TextEditor editor && GetUseApplicationTheme(editor)) {
                ApplyApplicationTheme(editor);
            }
        }

        private static void EditorOnTextChanged(object sender, EventArgs e) {
            var editor = (TextEditor)sender;
            SetBindableText(editor, editor.Text);
        }

        private static void ApplyApplicationTheme(TextEditor editor) {
            var background = GetApplicationBrush(editor, "SecondaryBackgroundBrush", editor.Background);
            var foreground = GetApplicationBrush(editor, "ButtonForegroundBrush", editor.Foreground);
            var mutedForeground = GetApplicationBrush(editor, "ButtonForegroundDisabledBrush", foreground);
            var border = GetApplicationBrush(editor, "BorderBrush", editor.BorderBrush);
            var currentLine = WithOpacity(GetApplicationBrush(editor, "TertiaryBackgroundBrush", background), 0.35);
            var selection = GetApplicationBrush(editor, "ButtonBackgroundSelectedBrush", currentLine);
            var selectionForeground = GetApplicationBrush(editor, "ButtonForegroundBrush", foreground);
            var accent = GetApplicationBrush(editor, "PrimaryBrush", foreground);

            editor.Background = background;
            editor.Foreground = foreground;
            editor.BorderBrush = border;
            editor.LineNumbersForeground = mutedForeground;

            editor.TextArea.Background = background;
            editor.TextArea.Foreground = foreground;
            editor.TextArea.BorderBrush = border;
            editor.TextArea.SelectionBrush = selection;
            editor.TextArea.SelectionForeground = selectionForeground;
            editor.TextArea.SelectionBorder = new Pen(accent, 1);
            editor.TextArea.Caret.CaretBrush = accent;
            editor.TextArea.TextView.CurrentLineBackground = currentLine;
            editor.TextArea.TextView.CurrentLineBorder = new Pen(border, 1);
            editor.TextArea.TextView.LinkTextForegroundBrush = accent;
            editor.TextArea.TextView.NonPrintableCharacterBrush = mutedForeground;
            editor.Options.HighlightCurrentLine = true;
        }

        private static Brush GetApplicationBrush(FrameworkElement element, string resourceKey, Brush fallback) {
            return element.TryFindResource(resourceKey) as Brush ?? fallback ?? Brushes.Transparent;
        }

        private static Brush WithOpacity(Brush brush, double opacity) {
            var clone = brush?.CloneCurrentValue() ?? Brushes.Transparent.CloneCurrentValue();
            clone.Opacity *= opacity;
            return clone;
        }
    }
}
