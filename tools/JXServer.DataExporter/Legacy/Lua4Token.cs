namespace JXServer.DataExporter.Legacy;

/// <summary>
/// Single token produced by <see cref="Lua4Lexer"/>.
/// </summary>
public readonly record struct Lua4Token(
    Lua4TokenKind Kind,
    string Text,
    double NumberValue,
    int Line);
