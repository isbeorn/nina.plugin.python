using NINA.Core.Model;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace NINA.Plugin.Python.PythonScriptingTestCategory {

    public enum PythonScriptSource {
        Inline,
        File
    }

    public sealed class PythonScriptSourceOption {
        public PythonScriptSourceOption(PythonScriptSource value, string label) {
            Value = value;
            Label = label;
        }

        public PythonScriptSource Value { get; }
        public string Label { get; }
    }

    public sealed class PythonScriptExecutionSource {
        public PythonScriptExecutionSource(string script, string filePath) {
            Script = script;
            FilePath = filePath;
        }

        public string Script { get; }
        public string FilePath { get; }
    }

    public static class PythonScriptSourceHelper {
        private static readonly Regex environmentVariablePattern = new Regex("%[^%]+%", RegexOptions.Compiled);

        public static IReadOnlyList<PythonScriptSourceOption> SourceOptions { get; } = new[] {
            new PythonScriptSourceOption(PythonScriptSource.Inline, "Inline"),
            new PythonScriptSourceOption(PythonScriptSource.File, "File")
        };

        public static string PreviewNotLoadedStatus => "Preview not loaded.";

        public static string SelectScriptFileToolTip(PythonScriptSource scriptSource) {
            return scriptSource == PythonScriptSource.File
                ? "Select external Python script file"
                : "Import script from file into inline editor";
        }

        public static PythonScriptExecutionSource GetScriptExecutionSource(
            PythonScriptSource scriptSource,
            string inlineScript,
            string scriptFilePath) {
            if (scriptSource == PythonScriptSource.Inline) {
                return new PythonScriptExecutionSource(inlineScript ?? string.Empty, null);
            }

            string resolvedPath = ResolveAbsoluteFilePath(scriptFilePath);
            try {
                return new PythonScriptExecutionSource(ReadAllTextUtf8(resolvedPath), resolvedPath);
            } catch (Exception ex) {
                throw new SequenceEntityFailedException($"Failed to read Python script file '{resolvedPath}': {ex.Message}", ex);
            }
        }

        public static string ReadScriptFile(string scriptFilePath) {
            string resolvedPath = ResolveAbsoluteFilePath(scriptFilePath);
            return ReadAllTextUtf8(resolvedPath);
        }

        public static string ImportScriptFile(string scriptFilePath) {
            return ReadAllTextUtf8(scriptFilePath);
        }

        public static bool TryValidateScriptSource(
            PythonScriptSource scriptSource,
            string scriptFilePath,
            out string issue) {
            issue = string.Empty;

            if (scriptSource == PythonScriptSource.Inline) {
                return true;
            }

            return TryValidateFilePathSyntax(scriptFilePath, out issue);
        }

        public static bool TryValidateFilePathSyntax(string scriptFilePath, out string issue) {
            issue = string.Empty;

            if (string.IsNullOrWhiteSpace(scriptFilePath)) {
                issue = "Python script file path must not be empty.";
                return false;
            }

            string trimmedPath = scriptFilePath.Trim();
            if (UsesUnsupportedPathShortcut(trimmedPath)) {
                issue = "Environment variables and path shortcuts are not supported. Use a fully qualified path.";
                return false;
            }

            if (!Path.IsPathFullyQualified(trimmedPath)) {
                issue = "Python script file path must be absolute.";
                return false;
            }

            try {
                Path.GetFullPath(trimmedPath);
                return true;
            } catch (Exception ex) {
                issue = $"Python script file path is not valid: {ex.Message}";
                return false;
            }
        }

        public static string ResolveAbsoluteFilePath(string scriptFilePath) {
            if (!TryValidateFilePathSyntax(scriptFilePath, out string issue)) {
                throw new SequenceEntityFailedException(issue);
            }

            return Path.GetFullPath(scriptFilePath.Trim());
        }

        private static bool UsesUnsupportedPathShortcut(string scriptFilePath) {
            return scriptFilePath.StartsWith("~", StringComparison.Ordinal)
                || scriptFilePath.StartsWith("$env:", StringComparison.OrdinalIgnoreCase)
                || environmentVariablePattern.IsMatch(scriptFilePath);
        }

        private static string ReadAllTextUtf8(string path) {
            using var reader = new StreamReader(path, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
            return reader.ReadToEnd();
        }
    }
}
