using System.Text;

namespace LangProcessor;

public partial class MainForm : Form
{
    private const string AppTitle = "Языковой процессор";
    private const string UntitledName = "Новый документ";
    private const float DefaultFontSize = 11f;
    private const string FileFilter =
        "Текстовые файлы (*.txt)|*.txt|" +
        "Все файлы (*.*)|*.*";

    private readonly LineNumberPanel _lineNumbers;
    private readonly Scanner _scanner = new();
    private string? _filePath;
    private string _savedText = "";
    private float _fontSize = DefaultFontSize;

    public MainForm() : this(Array.Empty<string>()) { }

    public MainForm(string[] args)
    {
        InitializeComponent();

        codeEditor.AllowDrop = true;
        codeEditor.DragEnter += OnDragEnter;
        codeEditor.DragDrop += OnDragDrop;
        codeEditor.ZoomRequested += (_, delta) => SetFontSize(_fontSize + delta);

        cmbFontSize.SelectedItem = ((int)DefaultFontSize).ToString();

        _lineNumbers = new LineNumberPanel(codeEditor);
        splitContainer.Panel1.Controls.Add(_lineNumbers);

        var file = args.FirstOrDefault(File.Exists);
        if (file is not null) LoadFile(file);
        else NewDocument();
    }

    private bool IsModified => codeEditor.CurrentText != _savedText;
    private string DisplayName => _filePath is null ? UntitledName : Path.GetFileName(_filePath);

    private void OnEditorTextChanged(object? sender, EventArgs e) => UpdateUi();

    private void OnEditorSelectionChanged(object? sender, EventArgs e) => UpdateCaretStatus();

    private void UpdateUi()
    {
        Text = $"{AppTitle} — {DisplayName}{(IsModified ? " *" : "")}";
        btnUndo.Enabled = menuUndo.Enabled = codeEditor.CanUndoEdit;
        btnRedo.Enabled = menuRedo.Enabled = codeEditor.CanRedoEdit;
        UpdateCaretStatus();
    }

    private void UpdateCaretStatus()
    {
        var text = codeEditor.CurrentText;
        var pos = Math.Min(codeEditor.SelectionStart, text.Length);
        int line = 1, lineStart = 0, lines = 1;
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] != '\n') continue;
            lines++;
            if (i < pos) { line++; lineStart = i + 1; }
        }

        var selected = codeEditor.SelectionLength;
        statusPosition.Text = $"Стр {line}, Стлб {pos - lineStart + 1}" + (selected > 0 ? $" (выделено {selected})" : "");
        statusLength.Text = $"Строк: {lines}   Символов: {text.Length}";
    }

    private void SetStatus(string text) => statusText.Text = text;

    private bool ConfirmSave()
    {
        if (!IsModified) return true;

        var answer = MessageBox.Show(this,
            $"Сохранить изменения в файле «{DisplayName}»?",
            AppTitle, MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);

        return answer switch
        {
            DialogResult.Yes => SaveDocument(saveAs: false),
            DialogResult.No => true,
            _ => false,
        };
    }

    private void NewDocument()
    {
        _filePath = null;
        codeEditor.LoadText("");
        _savedText = codeEditor.CurrentText;
        ClearResults();
        UpdateUi();
        codeEditor.Focus();
    }

    private void OnNew(object? sender, EventArgs e)
    {
        if (!ConfirmSave()) return;
        NewDocument();
        SetStatus("Создан новый документ");
    }

    private void OnOpen(object? sender, EventArgs e)
    {
        if (!ConfirmSave()) return;

        using var dialog = new OpenFileDialog { Title = "Открыть файл", Filter = FileFilter };
        if (_filePath is not null) dialog.InitialDirectory = Path.GetDirectoryName(_filePath);
        if (dialog.ShowDialog(this) == DialogResult.OK)
            LoadFile(dialog.FileName);
    }

    private bool LoadFile(string path)
    {
        string text;
        try
        {
            text = File.ReadAllText(path, Encoding.UTF8);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Не удалось открыть файл:\n{path}\n\n{ex.Message}",
                "Ошибка открытия", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return false;
        }

        _filePath = Path.GetFullPath(path);
        codeEditor.LoadText(text);
        _savedText = codeEditor.CurrentText;
        ClearResults();
        UpdateUi();
        codeEditor.Focus();
        SetStatus($"Открыт файл: {_filePath}");
        return true;
    }

    private void OnSave(object? sender, EventArgs e) => SaveDocument(saveAs: false);

    private void OnSaveAs(object? sender, EventArgs e) => SaveDocument(saveAs: true);

    private bool SaveDocument(bool saveAs)
    {
        var path = _filePath;
        if (saveAs || path is null)
        {
            using var dialog = new SaveFileDialog
            {
                Title = "Сохранить как",
                Filter = FileFilter,
                DefaultExt = "txt",
                AddExtension = true,
                OverwritePrompt = true,
                FileName = path is null ? "program.txt" : Path.GetFileName(path),
            };
            if (path is not null) dialog.InitialDirectory = Path.GetDirectoryName(path);
            if (dialog.ShowDialog(this) != DialogResult.OK) return false;
            path = dialog.FileName;
        }

        try
        {
            var text = codeEditor.CurrentText;
            File.WriteAllText(path, text.Replace("\n", Environment.NewLine), new UTF8Encoding(false));
            _savedText = text;
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Не удалось сохранить файл:\n{path}\n\n{ex.Message}",
                "Ошибка сохранения", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return false;
        }

        _filePath = path;
        UpdateUi();
        SetStatus($"Сохранено: {path}");
        return true;
    }

    private void OnExit(object? sender, EventArgs e) => Close();

    private void OnFormClosing(object? sender, FormClosingEventArgs e)
    {
        if (!ConfirmSave()) e.Cancel = true;
    }

    private void OnDragEnter(object? sender, DragEventArgs e) =>
        e.Effect = e.Data?.GetDataPresent(DataFormats.FileDrop) == true ? DragDropEffects.Copy : DragDropEffects.None;

    private void OnDragDrop(object? sender, DragEventArgs e)
    {
        if (e.Data?.GetData(DataFormats.FileDrop) is not string[] files) return;
        var path = files.FirstOrDefault(File.Exists);
        if (path is not null && ConfirmSave()) LoadFile(path);
    }

    private void EditCommand(Action<CodeEditor> action)
    {
        action(codeEditor);
        codeEditor.Focus();
        UpdateUi();
    }

    private void OnUndo(object? sender, EventArgs e) => EditCommand(ed => ed.UndoEdit());
    private void OnRedo(object? sender, EventArgs e) => EditCommand(ed => ed.RedoEdit());
    private void OnCut(object? sender, EventArgs e) => EditCommand(ed => ed.CutSelection());
    private void OnPaste(object? sender, EventArgs e) => EditCommand(ed => ed.PastePlain());
    private void OnDelete(object? sender, EventArgs e) => EditCommand(ed => ed.DeleteSelection());
    private void OnSelectAll(object? sender, EventArgs e) => EditCommand(ed => ed.SelectAll());

    private void OnCopy(object? sender, EventArgs e)
    {
        if (outputGrid.Focused && outputGrid.CurrentRow?.Tag is Token l)
        {
            Clipboard.SetText($"{l.CodeText}\t{l.Type}\t{l.Text}\t{l.Location}");
            return;
        }
        EditCommand(ed => { if (ed.SelectionLength > 0) ed.Copy(); });
    }

    private void SetFontSize(float size)
    {
        _fontSize = Math.Clamp(size, 7f, 40f);
        codeEditor.Font = new Font(codeEditor.Font.FontFamily, _fontSize);

        var text = ((int)_fontSize).ToString();
        _updatingFontCombo = true;
        cmbFontSize.SelectedItem = cmbFontSize.Items.Contains(text) ? text : null;
        _updatingFontCombo = false;
        SetStatus($"Размер шрифта: {_fontSize}");
    }

    private bool _updatingFontCombo;

    private void OnFontSizeSelected(object? sender, EventArgs e)
    {
        if (_updatingFontCombo || cmbFontSize.SelectedItem is not string text) return;
        SetFontSize(float.Parse(text));
        codeEditor.Focus();
    }

    private void OnZoomIn(object? sender, EventArgs e) => SetFontSize(_fontSize + 1);
    private void OnZoomOut(object? sender, EventArgs e) => SetFontSize(_fontSize - 1);
    private void OnZoomReset(object? sender, EventArgs e) => SetFontSize(DefaultFontSize);

    private void OnToggleWordWrap(object? sender, EventArgs e) => codeEditor.WordWrap = menuWordWrap.Checked;
    private void OnToggleLineNumbers(object? sender, EventArgs e) => _lineNumbers.Visible = menuLineNumbers.Checked;

    private void OnClearOutput(object? sender, EventArgs e) => ClearResults();

    private void ClearResults() => outputGrid.Rows.Clear();

    private void OnRun(object? sender, EventArgs e)
    {
        ClearResults();
        var lexemes = _scanner.Analyze(codeEditor.CurrentText);

        foreach (var lexeme in lexemes)
        {
            var index = outputGrid.Rows.Add(lexeme.CodeText, lexeme.Type, lexeme.Text, lexeme.Location);
            var row = outputGrid.Rows[index];
            row.Tag = lexeme;
            if (lexeme.IsError)
                row.DefaultCellStyle.ForeColor = Color.FromArgb(200, 30, 30);
        }

        var errors = lexemes.Count(l => l.IsError);
        var summaryIndex = outputGrid.Rows.Add(
            "", $"Всего лексем: {lexemes.Count}",
            errors == 0 ? "Ошибок не обнаружено" : $"Недопустимых символов: {errors}", "");
        var summary = outputGrid.Rows[summaryIndex].DefaultCellStyle;
        summary.BackColor = SystemColors.Control;
        summary.SelectionBackColor = SystemColors.Control;
        summary.SelectionForeColor = errors == 0 ? Color.FromArgb(30, 120, 50) : Color.FromArgb(200, 30, 30);
        summary.ForeColor = summary.SelectionForeColor;
        summary.Font = new Font(outputGrid.Font, FontStyle.Bold);

        outputGrid.ClearSelection();
        SetStatus(errors == 0
            ? $"Лексический анализ завершён: лексем — {lexemes.Count}, ошибок нет"
            : $"Лексический анализ завершён: лексем — {lexemes.Count}, недопустимых символов — {errors}");
    }

    private void OnOutputCellClick(object? sender, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex >= 0 && outputGrid.Rows[e.RowIndex].Tag is Token lexeme)
            NavigateTo(lexeme);
    }

    private void OnOutputKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode != Keys.Enter || outputGrid.CurrentRow?.Tag is not Token lexeme) return;
        e.Handled = true;
        NavigateTo(lexeme);
    }

    private void NavigateTo(Token lexeme)
    {
        var length = codeEditor.TextLength;
        var offset = Math.Min(lexeme.Offset, length);
        codeEditor.Select(offset, Math.Min(lexeme.Length, length - offset));
        codeEditor.ScrollToCaret();
        BeginInvoke(() => codeEditor.Focus());
    }

    private void OnTextSection(object? sender, EventArgs e)
    {
        var item = sender as ToolStripItem;
        var sections = HelpContent.TextSection(item?.Tag as string ?? "");
        InfoForm.Show(this, item?.Text ?? "", sections, new Size(720, 600));
    }

    private void OnHelp(object? sender, EventArgs e) =>
        InfoForm.Show(this, "Справка — " + AppTitle, HelpContent.UserGuide, new Size(720, 600));

    private void OnAbout(object? sender, EventArgs e) =>
        InfoForm.Show(this, "О программе", HelpContent.About, new Size(540, 400));
}
