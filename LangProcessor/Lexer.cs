namespace LangProcessor;

public sealed record Lexeme(int Code, string Type, string Text, int Line, int StartColumn, int EndColumn, int Offset, int Length)
{
    public bool IsError => Code == Lexer.ErrorCode;
    public string CodeText => IsError ? "ERROR" : Code.ToString();
    public string Location => $"строка {Line}, {StartColumn}-{EndColumn}";
}

public static class Lexer
{
    public const int ErrorCode = 0;

    public const int KeywordVar = 1;
    public const int KeywordConst = 2;
    public const int KeywordComplex64 = 3;
    public const int KeywordComplex128 = 4;
    public const int KeywordComplex = 5;
    public const int Identifier = 6;
    public const int Integer = 7;
    public const int Real = 8;
    public const int Imaginary = 9;
    public const int Assign = 10;
    public const int ShortAssign = 11;
    public const int Plus = 12;
    public const int Minus = 13;
    public const int Multiply = 14;
    public const int Divide = 15;
    public const int Space = 16;
    public const int LeftParen = 17;
    public const int RightParen = 18;
    public const int Comma = 19;
    public const int Semicolon = 20;

    private static readonly Dictionary<string, int> Keywords = new(StringComparer.Ordinal)
    {
        ["var"] = KeywordVar,
        ["const"] = KeywordConst,
        ["complex64"] = KeywordComplex64,
        ["complex128"] = KeywordComplex128,
        ["complex"] = KeywordComplex,
    };

    public static readonly (int Code, string Type, string Example)[] Table =
    {
        (KeywordVar, "ключевое слово", "var"),
        (KeywordConst, "ключевое слово", "const"),
        (KeywordComplex64, "ключевое слово", "complex64"),
        (KeywordComplex128, "ключевое слово", "complex128"),
        (KeywordComplex, "ключевое слово", "complex"),
        (Identifier, "идентификатор", "z, c1, my_num"),
        (Integer, "целое без знака", "3, 128"),
        (Real, "вещественное число", "2.5, 0.75"),
        (Imaginary, "мнимое число", "4i, 1.5i"),
        (Assign, "оператор присваивания", "="),
        (ShortAssign, "оператор краткого объявления", ":="),
        (Plus, "оператор сложения", "+"),
        (Minus, "оператор вычитания", "-"),
        (Multiply, "оператор умножения", "*"),
        (Divide, "оператор деления", "/"),
        (Space, "разделитель (пробел)", "пробел, табуляция"),
        (LeftParen, "открывающая скобка", "("),
        (RightParen, "закрывающая скобка", ")"),
        (Comma, "запятая", ","),
        (Semicolon, "конец оператора", ";"),
    };

    private static bool IsLetter(char c) => c is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or '_';
    private static bool IsDigit(char c) => c is >= '0' and <= '9';

    public static List<Lexeme> Scan(string text)
    {
        var result = new List<Lexeme>();
        var line = 1;
        var lineStart = 0;
        var i = 0;

        void Add(int code, string type, string lexeme, int start, int end) =>
            result.Add(new Lexeme(code, type, lexeme, line, start - lineStart + 1, end - lineStart, start, end - start));

        while (i < text.Length)
        {
            var c = text[i];
            var start = i;

            if (c == '\n')
            {
                i++;
                line++;
                lineStart = i;
                continue;
            }
            if (c == '\r')
            {
                i++;
                continue;
            }

            if (IsLetter(c))
            {
                while (i < text.Length && (IsLetter(text[i]) || IsDigit(text[i]))) i++;
                var word = text[start..i];
                if (Keywords.TryGetValue(word, out var code))
                    Add(code, "ключевое слово", word, start, i);
                else
                    Add(Identifier, "идентификатор", word, start, i);
                continue;
            }

            if (IsDigit(c))
            {
                while (i < text.Length && IsDigit(text[i])) i++;
                var code = Integer;
                if (i + 1 < text.Length && text[i] == '.' && IsDigit(text[i + 1]))
                {
                    i++;
                    while (i < text.Length && IsDigit(text[i])) i++;
                    code = Real;
                }
                if (i < text.Length && text[i] == 'i')
                {
                    i++;
                    code = Imaginary;
                }
                var type = code switch
                {
                    Integer => "целое без знака",
                    Real => "вещественное число",
                    _ => "мнимое число",
                };
                Add(code, type, text[start..i], start, i);
                continue;
            }

            if (c == ':' && i + 1 < text.Length && text[i + 1] == '=')
            {
                i += 2;
                Add(ShortAssign, "оператор краткого объявления", ":=", start, i);
                continue;
            }

            i++;
            switch (c)
            {
                case ' ':
                    Add(Space, "разделитель (пробел)", "(пробел)", start, i);
                    break;
                case '\t':
                    Add(Space, "разделитель (пробел)", "(табуляция)", start, i);
                    break;
                case '=':
                    Add(Assign, "оператор присваивания", "=", start, i);
                    break;
                case '+':
                    Add(Plus, "оператор сложения", "+", start, i);
                    break;
                case '-':
                    Add(Minus, "оператор вычитания", "-", start, i);
                    break;
                case '*':
                    Add(Multiply, "оператор умножения", "*", start, i);
                    break;
                case '/':
                    Add(Divide, "оператор деления", "/", start, i);
                    break;
                case '(':
                    Add(LeftParen, "открывающая скобка", "(", start, i);
                    break;
                case ')':
                    Add(RightParen, "закрывающая скобка", ")", start, i);
                    break;
                case ',':
                    Add(Comma, "запятая", ",", start, i);
                    break;
                case ';':
                    Add(Semicolon, "конец оператора", ";", start, i);
                    break;
                default:
                    Add(ErrorCode, "недопустимый символ", c.ToString(), start, i);
                    break;
            }
        }

        return result;
    }
}
