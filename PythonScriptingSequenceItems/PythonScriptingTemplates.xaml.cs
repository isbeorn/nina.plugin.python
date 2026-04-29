using ICSharpCode.AvalonEdit;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace NINA.Plugin.Python.PythonScriptingTestCategory {

    [Export(typeof(ResourceDictionary))]
    public partial class PluginItemTemplate : ResourceDictionary {

        public PluginItemTemplate() {
            InitializeComponent();
        }
    }

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

        private static void OnBindableTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
            if (d is not TextEditor editor)
                return;

            editor.TextChanged -= EditorOnTextChanged;

            var newText = e.NewValue as string ?? "";
            if (editor.Text != newText)
                editor.Text = newText;

            editor.TextChanged += EditorOnTextChanged;
        }

        private static void EditorOnTextChanged(object sender, EventArgs e) {
            var editor = (TextEditor)sender;
            SetBindableText(editor, editor.Text);
        }
    }
}