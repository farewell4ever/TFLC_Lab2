namespace LangProcessor;

public sealed record Token(int Code, string Type, string Text, int Line, int StartColumn, int EndColumn, int Offset, int Length)
{
    public bool IsError => Code == Scanner.ErrorCode;
    public string CodeText => IsError ? "ERROR" : Code.ToString();
    public string Location => $"строка {Line}, {StartColumn}-{EndColumn}";
}

public enum ScannerState
{
    Start = 0,
    Word = 1,
    Integer = 3,
    Dot = 4,
    Fraction = 5,
    Imaginary = 6,
    Colon = 7,
}

public sealed class Scanner
{
    public const int ErrorCode = 0;

    public const int Integer = 1;
    public const int Identifier = 2;
    public const int Real = 3;
    public const int Imaginary = 4;
    public const int Plus = 5;
    public const int Minus = 6;
    public const int Multiply = 7;
    public const int Divide = 8;
    public const int ShortAssign = 9;
    public const int Assign = 10;
    public const int Space = 11;
    public const int LeftParen = 12;
    public const int RightParen = 13;
    public const int Keyword = 14;
    public const int Comma = 15;
    public const int Semicolon = 16;

    private static readonly HashSet<string> Keywords = new(StringComparer.Ordinal)
    {
        "var", "const", "complex64", "complex128", "complex",
    };

    private static readonly Dictionary<char, (int Code, string Type)> SingleCharTokens = new()
    {
        [' '] = (Space, "разделитель (пробел)"),
        ['\t'] = (Space, "разделитель (пробел)"),
        ['='] = (Assign, "оператор присваивания"),
        ['+'] = (Plus, "оператор сложения"),
        ['-'] = (Minus, "оператор вычитания"),
        ['*'] = (Multiply, "оператор умножения"),
        ['/'] = (Divide, "оператор деления"),
        ['('] = (LeftParen, "открывающая скобка"),
        [')'] = (RightParen, "закрывающая скобка"),
        [','] = (Comma, "запятая"),
        [';'] = (Semicolon, "конец оператора"),
    };

    private readonly List<Token> _tokens = new();
    private string _text = "";
    private int _line;
    private int _lineStart;
    private int _start;

    private static bool IsLetter(char c) => c is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or '_';
    private static bool IsDigit(char c) => c is >= '0' and <= '9';

    public List<Token> Analyze(string text)
    {
        _tokens.Clear();
        _text = text;
        _line = 1;
        _lineStart = 0;

        var state = ScannerState.Start;
        var i = 0;

        while (true)
        {
            var c = i < text.Length ? text[i] : '\0';

            switch (state)
            {
                case ScannerState.Start:
                    if (i >= text.Length) return new List<Token>(_tokens);
                    _start = i;
                    if (c == '\n')
                    {
                        i++;
                        _line++;
                        _lineStart = i;
                    }
                    else if (c == '\r') i++;
                    else if (IsLetter(c)) { state = ScannerState.Word; i++; }
                    else if (IsDigit(c)) { state = ScannerState.Integer; i++; }
                    else if (c == ':') { state = ScannerState.Colon; i++; }
                    else if (SingleCharTokens.TryGetValue(c, out var single))
                    {
                        i++;
                        Emit(single.Code, single.Type, i, c == ' ' ? "(пробел)" : c == '\t' ? "(табуляция)" : null);
                    }
                    else
                    {
                        i++;
                        Emit(ErrorCode, "недопустимый символ", i);
                    }
                    break;

                case ScannerState.Word:
                    if (IsLetter(c) || IsDigit(c)) i++;
                    else
                    {
                        var word = text[_start..i];
                        if (Keywords.Contains(word)) Emit(Keyword, "ключевое слово", i);
                        else Emit(Identifier, "идентификатор", i);
                        state = ScannerState.Start;
                    }
                    break;

                case ScannerState.Integer:
                    if (IsDigit(c)) i++;
                    else if (c == '.' && i + 1 < text.Length && IsDigit(text[i + 1])) { state = ScannerState.Dot; i++; }
                    else if (c == 'i') { state = ScannerState.Imaginary; i++; }
                    else
                    {
                        Emit(Integer, "целое без знака", i);
                        state = ScannerState.Start;
                    }
                    break;

                case ScannerState.Dot:
                    state = ScannerState.Fraction;
                    i++;
                    break;

                case ScannerState.Fraction:
                    if (IsDigit(c)) i++;
                    else if (c == 'i') { state = ScannerState.Imaginary; i++; }
                    else
                    {
                        Emit(Real, "вещественное число", i);
                        state = ScannerState.Start;
                    }
                    break;

                case ScannerState.Imaginary:
                    Emit(Imaginary, "мнимое число", i);
                    state = ScannerState.Start;
                    break;

                case ScannerState.Colon:
                    if (c == '=')
                    {
                        i++;
                        Emit(ShortAssign, "оператор краткого объявления", i);
                    }
                    else Emit(ErrorCode, "недопустимый символ", i);
                    state = ScannerState.Start;
                    break;
            }
        }
    }

    private void Emit(int code, string type, int end, string? shownText = null) =>
        _tokens.Add(new Token(code, type, shownText ?? _text[_start..end], _line,
            _start - _lineStart + 1, end - _lineStart, _start, end - _start));
}
