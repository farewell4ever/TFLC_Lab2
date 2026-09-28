using LangProcessor;
using Xunit;

namespace LangProcessor.Tests;

public class ScannerTests
{
    private static List<Token> Analyze(string text) => new Scanner().Analyze(text);

    private static int[] Codes(string text) => Analyze(text).Select(t => t.Code).ToArray();

    [Theory]
    [InlineData("123", Scanner.Integer)]
    [InlineData("2.5", Scanner.Real)]
    [InlineData("4i", Scanner.Imaginary)]
    [InlineData("1.5i", Scanner.Imaginary)]
    public void Numbers(string text, int code)
    {
        var tokens = Analyze(text);
        Assert.Single(tokens);
        Assert.Equal(code, tokens[0].Code);
        Assert.Equal(text, tokens[0].Text);
    }

    [Theory]
    [InlineData("z")]
    [InlineData("num_1")]
    [InlineData("_tmp")]
    [InlineData("complex3")]
    public void Identifiers(string text)
    {
        var token = Assert.Single(Analyze(text));
        Assert.Equal(Scanner.Identifier, token.Code);
        Assert.Equal("идентификатор", token.Type);
    }

    [Theory]
    [InlineData("var")]
    [InlineData("const")]
    [InlineData("complex64")]
    [InlineData("complex128")]
    [InlineData("complex")]
    public void Keywords(string text)
    {
        var token = Assert.Single(Analyze(text));
        Assert.Equal(Scanner.Keyword, token.Code);
        Assert.Equal("ключевое слово", token.Type);
    }

    [Theory]
    [InlineData("=", Scanner.Assign)]
    [InlineData(":=", Scanner.ShortAssign)]
    [InlineData("+", Scanner.Plus)]
    [InlineData("-", Scanner.Minus)]
    [InlineData("*", Scanner.Multiply)]
    [InlineData("/", Scanner.Divide)]
    public void Operators(string text, int code)
    {
        var token = Assert.Single(Analyze(text));
        Assert.Equal(code, token.Code);
    }

    [Fact]
    public void SpacesAndSeparators()
    {
        Assert.Equal(new[] { Scanner.LeftParen, Scanner.Integer, Scanner.Comma, Scanner.Space, Scanner.Integer, Scanner.RightParen, Scanner.Semicolon },
            Codes("(3, 4);"));
        var tab = Assert.Single(Analyze("\t"));
        Assert.Equal(Scanner.Space, tab.Code);
    }

    [Fact]
    public void TaskExample()
    {
        Assert.Equal(new[] { Scanner.Keyword, Scanner.Space, Scanner.Identifier, Scanner.Space, Scanner.Keyword, Scanner.Space,
                             Scanner.Assign, Scanner.Space, Scanner.Keyword, Scanner.LeftParen, Scanner.Integer, Scanner.Comma,
                             Scanner.Space, Scanner.Integer, Scanner.RightParen, Scanner.Semicolon },
            Codes("var z complex128 = complex(3, 4);"));
    }

    [Theory]
    [InlineData("@")]
    [InlineData("#")]
    [InlineData("я")]
    [InlineData(":")]
    [InlineData(".")]
    public void InvalidCharacter(string text)
    {
        var token = Assert.Single(Analyze(text));
        Assert.True(token.IsError);
        Assert.Equal("ERROR", token.CodeText);
        Assert.Equal("строка 1, 1-1", token.Location);
    }

    [Fact]
    public void ErrorDoesNotStopAnalysis()
    {
        var tokens = Analyze("var z complex64 = 1 @ 2i;");
        var error = Assert.Single(tokens, t => t.IsError);
        Assert.Equal("@", error.Text);
        Assert.Equal(20, error.Offset);
        Assert.Equal("строка 1, 21-21", error.Location);
        Assert.Equal(Scanner.Semicolon, tokens[^1].Code);
    }

    [Fact]
    public void DotWithoutDigitIsError()
    {
        var tokens = Analyze("3.;");
        Assert.Equal(new[] { Scanner.Integer, Scanner.ErrorCode, Scanner.Semicolon }, tokens.Select(t => t.Code));
    }

    [Fact]
    public void Multiline()
    {
        var tokens = Analyze("var a complex128;\nconst b = 3i;\n  c := a @ b");
        var constToken = tokens.First(t => t.Text == "const");
        Assert.Equal("строка 2, 1-5", constToken.Location);
        var shortAssign = tokens.First(t => t.Code == Scanner.ShortAssign);
        Assert.Equal("строка 3, 5-6", shortAssign.Location);
        var error = Assert.Single(tokens, t => t.IsError);
        Assert.Equal(3, error.Line);
        Assert.Equal(10, error.StartColumn);
    }

    [Fact]
    public void RepeatedAnalysisStartsClean()
    {
        var scanner = new Scanner();
        scanner.Analyze("var a\nvar b");
        var tokens = scanner.Analyze("x");
        var token = Assert.Single(tokens);
        Assert.Equal(1, token.Line);
    }
}
