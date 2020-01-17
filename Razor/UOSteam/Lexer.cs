﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace UOSteam
{
    public enum ASTNodeType
    {
        // Keywords
        IF,
        ELSEIF,
        ENDIF,
        WHILE,
        ENDWHILE,
        FOR,
        ENDFOR,
        BREAK,
        CONTINUE,
        STOP,
        REPLAY,

        // Operators
        NOT,
        EQUAL,
        NOT_EQUAL,
        LESS_THAN,
        LESS_THAN_OR_EQUAL,
        GREATER_THAN,
        GREATER_THAN_OR_EQUAL,

        // Value types
        STRING,
        SERIAL,
        INTEGER,

        // Modifiers
        QUIET, // @ symbol
        FORCE, // ! symbol
        
        // Everything else
        SCRIPT,
        STATEMENT,
        COMMAND,
        UNARY_EXPRESSION,
        BINARY_EXPRESSION,
    }

    // Abstract Syntax Tree Node
    public class ASTNode
    {
        public readonly ASTNodeType Type;
        public readonly string Lexeme;
        public readonly ASTNode Parent;

        internal LinkedListNode<ASTNode> _node;
        private LinkedList<ASTNode> _children;

        public ASTNode(ASTNodeType type, string lexeme, ASTNode parent)
        {
            Type = type;
            if (lexeme != null)
                Lexeme = lexeme;
            else
                Lexeme = "";
            Parent = parent;
        }

        public ASTNode Push(ASTNodeType type, string lexeme)
        {
            var node = new ASTNode(type, lexeme, this);

            if (_children == null)
                _children = new LinkedList<ASTNode>();

            node._node = _children.AddLast(node);

            return node;
        }

        public ASTNode FirstChild()
        {
            if (_children == null || _children.First == null)
                return null;

            return _children.First.Value;
        }

        public ASTNode Next()
        {
            if (_node == null || _node.Next == null)
                return null;

            return _node.Next.Value;
        }

        public ASTNode Prev()
        {
            if (_node == null || _node.Previous == null)
                return null;

            return _node.Previous.Value;
        }
    }

    public static class Lexer
    {
        public static ASTNode Lex(string fname)
        {
            ASTNode node = new ASTNode(ASTNodeType.SCRIPT, null, null);

            using (var file = new StreamReader(fname))
            {
                while (true)
                {
                    // Each line in the file is a statement. Statements starting
                    // with a control flow keyword contain an expression.

                    var line = file.ReadLine();

                    // End of file
                    if (line == null)
                        break;

                    line = line.Trim();

                    if (line.StartsWith("//") || line.StartsWith('#'))
                        continue;

                    // Split the line by spaces (unless the space is in quotes)
                    var lexemes = line.Split('\'')
                                   .Select((element, index) => index % 2 == 0 ?
                                    element.Split(new char[0], StringSplitOptions.RemoveEmptyEntries) :
                                    new string[] { element })
                                   .SelectMany(element => element).ToArray();

                    if (lexemes.Length == 0)
                        continue;

                    ParseStatement(ref node, lexemes);
                }
            }

            return node;
        }

        private static void ParseValue(ref ASTNode node, string lexeme)
        {
            if (lexeme.StartsWith("0x"))
                node.Push(ASTNodeType.SERIAL, lexeme);
            else if (int.TryParse(lexeme, out _))
                node.Push(ASTNodeType.INTEGER, lexeme);
            else
                node.Push(ASTNodeType.STRING, lexeme);
        }

        private static void ParseCommand(ref ASTNode node, string lexeme)
        {
            // A command may start with an '@' symbol. Pick that
            // off.
            if (lexeme[0] == '@')
            {
                node.Push(ASTNodeType.QUIET, null);
                lexeme = lexeme[1..^0];
            }

            // A command may end with a '!' symbol. Pick that
            // off.
            if (lexeme[^1] == '!')
            {
                node.Push(ASTNodeType.FORCE, null);
                lexeme = lexeme[0..^1];
            }

            node.Push(ASTNodeType.COMMAND, lexeme);
        }

        private static void ParseOperator(ref ASTNode node, string lexeme)
        {
            switch (lexeme)
            {
                case "==":
                case "=":
                    node.Push(ASTNodeType.EQUAL, null);
                    break;
                case "!=":
                    node.Push(ASTNodeType.NOT_EQUAL, null);
                    break;
                case "<":
                    node.Push(ASTNodeType.LESS_THAN, null);
                    break;
                case "<=":
                    node.Push(ASTNodeType.LESS_THAN_OR_EQUAL, null);
                    break;
                case ">":
                    node.Push(ASTNodeType.GREATER_THAN, null);
                    break;
                case ">=":
                    node.Push(ASTNodeType.GREATER_THAN_OR_EQUAL, null);
                    break;
                default:
                    throw new Exception("Invalid operator in binary expression");
            }
        }

        private static void ParseStatement(ref ASTNode node, string[] lexemes)
        {
            var statement = node.Push(ASTNodeType.STATEMENT, null);

            // Examine the first word on the line
            switch (lexemes[0])
            {
                // Ignore comments
                case "#":
                case "//":
                    return;

                // Control flow statements are special
                case "if":
                    {
                        if (lexemes.Length <= 1)
                            throw new Exception("Script compilation error");

                        var t = statement.Push(ASTNodeType.IF, null);
                        ParseExpression(ref t, lexemes[1..^0]);
                        break;
                    }
                case "elseif":
                    {
                        if (lexemes.Length <= 1)
                            throw new Exception("Script compilation error");

                        var t = statement.Push(ASTNodeType.ELSEIF, null);
                        ParseExpression(ref t, lexemes[1..^0]);
                        break;
                    }
                case "endif":
                    if (lexemes.Length > 1)
                        throw new Exception("Script compilation error");

                    statement.Push(ASTNodeType.ENDIF, null);
                    break;
                case "while":
                    {
                        if (lexemes.Length <= 1)
                            throw new Exception("Script compilation error");

                        var t = statement.Push(ASTNodeType.WHILE, null);
                        ParseExpression(ref t, lexemes[1..^0]);
                        break;
                    }
                case "endwhile":
                    if (lexemes.Length > 1)
                        throw new Exception("Script compilation error");

                    statement.Push(ASTNodeType.ENDWHILE, null);
                    break;
                case "for":
                    {
                        if (lexemes.Length <= 1)
                            throw new Exception("Script compilation error");

                        var t = statement.Push(ASTNodeType.FOR, null);
                        ParseExpression(ref t, lexemes[1..^0]);
                        break;
                    }
                case "endfor":
                    if (lexemes.Length > 1)
                        throw new Exception("Script compilation error");

                    statement.Push(ASTNodeType.ENDFOR, null);
                    break;
                case "break":
                    if (lexemes.Length > 1)
                        throw new Exception("Script compilation error");

                    statement.Push(ASTNodeType.BREAK, null);
                    break;
                case "continue":
                    if (lexemes.Length > 1)
                        throw new Exception("Script compilation error");

                    statement.Push(ASTNodeType.CONTINUE, null);
                    break;
                case "stop":
                    if (lexemes.Length > 1)
                        throw new Exception("Script compilation error");

                    statement.Push(ASTNodeType.STOP, null);
                    break;
                case "replay":
                    if (lexemes.Length > 1)
                        throw new Exception("Script compilation error");

                    statement.Push(ASTNodeType.REPLAY, null);
                    break;
                default:
                    // It's a regular statement.
                    ParseCommand(ref statement, lexemes[0]);

                    foreach (var lexeme in lexemes[1..^0])
                    {
                        ParseValue(ref statement, lexeme);
                    }
                    break;
            }

        }

        private static bool IsOperator(string lexeme)
        {
            switch (lexeme)
            {
                case "==":
                case "=":
                case "!=":
                case "<":
                case "<=":
                case ">":
                case ">=":
                    return true;
            }

            return false;
        }

        private static void ParseExpression(ref ASTNode node, string[] lexemes)
        {
            // The steam language supports both unary and
            // binary expressions. First determine what type
            // we have here.

            bool unary = false;
            bool binary = false;

            foreach (var lexeme in lexemes)
            {
                if (lexeme == "not")
                {
                    // The not lexeme only appears in unary expressions.
                    // Binary expressions would use "!=".
                    unary = true;
                }
                else if (IsOperator(lexeme))
                {
                    // Operators mean it is a binary expression.
                    binary = true;
                }
            }

            // If no operators appeared, it's a unary expression
            if (!unary && !binary)
                unary = true;

            if (unary && binary)
                throw new Exception("Invalid expression");

            if (unary)
                ParseUnaryExpression(ref node, lexemes);
            else
                ParseBinaryExpression(ref node, lexemes);
        }

        private static void ParseUnaryExpression(ref ASTNode node, string[] lexemes)
        {
            var expr = node.Push(ASTNodeType.UNARY_EXPRESSION, null);

            int i = 0;

            if (lexemes[i] == "not")
            {
                expr.Push(ASTNodeType.NOT, null);
                i++;
            }

            ParseCommand(ref expr, lexemes[i++]);

            for (; i < lexemes.Length; i++)
            {
                ParseValue(ref expr, lexemes[i]);
            }
        }

        private static void ParseBinaryExpression(ref ASTNode node, string[] lexemes)
        {
            var expr = node.Push(ASTNodeType.BINARY_EXPRESSION, null);

            int i = 0;

            ParseCommand(ref expr, lexemes[i++]);

            for (; i < lexemes.Length; i++)
            {
                if (IsOperator(lexemes[i]))
                    break;

                ParseValue(ref expr, lexemes[i]);
            }

            ParseOperator(ref expr, lexemes[i++]);

            ParseCommand(ref expr, lexemes[i++]);

            for (; i < lexemes.Length; i++)
            {
                if (IsOperator(lexemes[i]))
                    break;

                ParseValue(ref expr, lexemes[i]);
            }
        }
    }
}