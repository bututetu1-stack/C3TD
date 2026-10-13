using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

/// <summary>
/// 小さな JSON パーサー。JsonUtility では読めない「配列の配列」や「辞書」を読むために使う。
/// オブジェクトは Dictionary&lt;string, object&gt;、配列は List&lt;object&gt;、数値は double、
/// 真偽値は bool、null は null になる。
/// 手で書くデータなので、// と /* */ のコメントと、末尾の余分なカンマは許す。
/// </summary>
public static class MiniJson
{
    public static object Parse(string json)
    {
        var parser = new Parser(json);
        object value = parser.ParseValue();
        parser.SkipWhitespace();
        if (!parser.AtEnd) throw parser.Error("値のあとに余分な文字があります");
        return value;
    }

    private class Parser
    {
        private readonly string s;
        private int pos;

        public Parser(string text)
        {
            s = text ?? "";
            pos = 0;
        }

        public bool AtEnd { get { return pos >= s.Length; } }

        public FormatException Error(string message)
        {
            int line = 1, col = 1;
            for (int i = 0; i < pos && i < s.Length; i++)
            {
                if (s[i] == '\n') { line++; col = 1; }
                else col++;
            }
            return new FormatException(string.Format("{0}行{1}列: {2}", line, col, message));
        }

        public void SkipWhitespace()
        {
            while (pos < s.Length)
            {
                char c = s[pos];
                if (c == ' ' || c == '\t' || c == '\r' || c == '\n' || c == '﻿')
                {
                    pos++;
                    continue;
                }
                if (c == '/' && pos + 1 < s.Length && s[pos + 1] == '/')
                {
                    while (pos < s.Length && s[pos] != '\n') pos++;
                    continue;
                }
                if (c == '/' && pos + 1 < s.Length && s[pos + 1] == '*')
                {
                    int end = s.IndexOf("*/", pos + 2, StringComparison.Ordinal);
                    if (end < 0) throw Error("コメント /* が閉じていません");
                    pos = end + 2;
                    continue;
                }
                break;
            }
        }

        public object ParseValue()
        {
            SkipWhitespace();
            if (AtEnd) throw Error("値がありません");
            char c = s[pos];
            switch (c)
            {
                case '{': return ParseObject();
                case '[': return ParseArray();
                case '"': return ParseString();
                case 't': Expect("true"); return true;
                case 'f': Expect("false"); return false;
                case 'n': Expect("null"); return null;
            }
            if (c == '-' || (c >= '0' && c <= '9')) return ParseNumber();
            throw Error("予期しない文字 '" + c + "'");
        }

        private void Expect(string word)
        {
            if (pos + word.Length > s.Length || string.CompareOrdinal(s, pos, word, 0, word.Length) != 0)
                throw Error("'" + word + "' が必要です");
            pos += word.Length;
        }

        private Dictionary<string, object> ParseObject()
        {
            var dict = new Dictionary<string, object>();
            pos++; // {
            while (true)
            {
                SkipWhitespace();
                if (AtEnd) throw Error("'}' が必要です");
                if (s[pos] == '}')
                {
                    pos++;
                    return dict;
                }
                if (s[pos] != '"') throw Error("キー（\"...\"）が必要です");
                string key = ParseString();
                SkipWhitespace();
                if (AtEnd || s[pos] != ':') throw Error("キー \"" + key + "\" のあとに ':' が必要です");
                pos++;
                if (dict.ContainsKey(key)) throw Error("キー \"" + key + "\" が2回あります");
                dict[key] = ParseValue();
                SkipWhitespace();
                if (AtEnd) throw Error("'}' が必要です");
                if (s[pos] == ',') { pos++; continue; }
                if (s[pos] == '}') { pos++; return dict; }
                throw Error("',' か '}' が必要です");
            }
        }

        private List<object> ParseArray()
        {
            var list = new List<object>();
            pos++; // [
            while (true)
            {
                SkipWhitespace();
                if (AtEnd) throw Error("']' が必要です");
                if (s[pos] == ']')
                {
                    pos++;
                    return list;
                }
                list.Add(ParseValue());
                SkipWhitespace();
                if (AtEnd) throw Error("']' が必要です");
                if (s[pos] == ',') { pos++; continue; }
                if (s[pos] == ']') { pos++; return list; }
                throw Error("',' か ']' が必要です");
            }
        }

        private string ParseString()
        {
            var sb = new StringBuilder();
            pos++; // "
            while (true)
            {
                if (AtEnd) throw Error("文字列が閉じていません");
                char c = s[pos++];
                if (c == '"') return sb.ToString();
                if (c == '\n') throw Error("文字列の途中で改行しています");
                if (c != '\\')
                {
                    sb.Append(c);
                    continue;
                }
                if (AtEnd) throw Error("文字列が閉じていません");
                char e = s[pos++];
                switch (e)
                {
                    case '"': sb.Append('"'); break;
                    case '\\': sb.Append('\\'); break;
                    case '/': sb.Append('/'); break;
                    case 'b': sb.Append('\b'); break;
                    case 'f': sb.Append('\f'); break;
                    case 'n': sb.Append('\n'); break;
                    case 'r': sb.Append('\r'); break;
                    case 't': sb.Append('\t'); break;
                    case 'u':
                        if (pos + 4 > s.Length) throw Error("\\u のあとに16進数4桁が必要です");
                        int code;
                        if (!int.TryParse(s.Substring(pos, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out code))
                            throw Error("\\u のあとに16進数4桁が必要です");
                        sb.Append((char)code);
                        pos += 4;
                        break;
                    default:
                        throw Error("知らないエスケープ \\" + e);
                }
            }
        }

        private double ParseNumber()
        {
            int start = pos;
            while (pos < s.Length)
            {
                char c = s[pos];
                if ((c >= '0' && c <= '9') || c == '-' || c == '+' || c == '.' || c == 'e' || c == 'E') pos++;
                else break;
            }
            string text = s.Substring(start, pos - start);
            double value;
            if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
            {
                pos = start;
                throw Error("数値 '" + text + "' が読めません");
            }
            return value;
        }
    }
}
