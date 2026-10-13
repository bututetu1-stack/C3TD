using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using UnityEngine;

/// <summary>JSON のキー名が C# のフィールド名と違うとき（"class" など）に付ける</summary>
[AttributeUsage(AttributeTargets.Field)]
public class JsonKeyAttribute : Attribute
{
    public readonly string Key;
    public JsonKeyAttribute(string key) { Key = key; }
}

/// <summary>
/// MiniJson が返した値を、データクラスの public フィールドに詰める。
/// 対応する型：int / float / double / bool / string / enum（名前の文字列）/ 配列 / List / Dictionary&lt;string, T&gt; /
/// Vector2Int（[x, y]）/ object（そのまま入れる）/ それらを持つクラス。
/// JSON にないフィールドはクラスに書いた初期値のまま。知らないキーは警告を出して無視する。
/// </summary>
public static class JsonMapper
{
    private static readonly Dictionary<Type, Dictionary<string, FieldInfo>> fieldCache =
        new Dictionary<Type, Dictionary<string, FieldInfo>>();

    public static T To<T>(object node, string source)
    {
        return (T)ToType(node, typeof(T), source);
    }

    public static object ToType(object node, Type type, string path)
    {
        if (type == typeof(object)) return node;
        if (node == null) return type.IsValueType ? Activator.CreateInstance(type) : null;

        if (type == typeof(string))
        {
            var str = node as string;
            if (str != null) return str;
            if (node is double) return ((double)node).ToString(CultureInfo.InvariantCulture);
            throw Fail(path, "文字列が必要です");
        }
        if (type == typeof(int)) return (int)Math.Round(ToDouble(node, path));
        if (type == typeof(long)) return (long)Math.Round(ToDouble(node, path));
        if (type == typeof(float)) return (float)ToDouble(node, path);
        if (type == typeof(double)) return ToDouble(node, path);
        if (type == typeof(bool))
        {
            if (node is bool) return node;
            throw Fail(path, "true か false が必要です");
        }
        if (type.IsEnum) return ToEnum(node, type, path);
        if (type == typeof(Vector2Int))
        {
            var xy = AsList(node, path);
            if (xy.Count != 2) throw Fail(path, "[x, y] の形で書いてください");
            return new Vector2Int((int)Math.Round(ToDouble(xy[0], path + "[0]")), (int)Math.Round(ToDouble(xy[1], path + "[1]")));
        }
        if (type.IsArray)
        {
            var list = AsList(node, path);
            Type elem = type.GetElementType();
            Array array = Array.CreateInstance(elem, list.Count);
            for (int i = 0; i < list.Count; i++) array.SetValue(ToType(list[i], elem, path + "[" + i + "]"), i);
            return array;
        }
        if (type.IsGenericType)
        {
            Type def = type.GetGenericTypeDefinition();
            Type[] args = type.GetGenericArguments();
            if (def == typeof(List<>))
            {
                var src = AsList(node, path);
                var list = (IList)Activator.CreateInstance(type);
                for (int i = 0; i < src.Count; i++) list.Add(ToType(src[i], args[0], path + "[" + i + "]"));
                return list;
            }
            if (def == typeof(Dictionary<,>))
            {
                if (args[0] != typeof(string)) throw Fail(path, "辞書のキーは文字列だけに対応しています");
                var src = AsObject(node, path);
                var dict = (IDictionary)Activator.CreateInstance(type);
                foreach (var kv in src) dict[kv.Key] = ToType(kv.Value, args[1], path + "." + kv.Key);
                return dict;
            }
        }
        if (type.IsClass)
        {
            var src = AsObject(node, path);
            object result = Activator.CreateInstance(type);
            var fields = FieldsOf(type);
            foreach (var kv in src)
            {
                FieldInfo field;
                if (!fields.TryGetValue(kv.Key.ToLowerInvariant(), out field))
                {
                    Debug.LogWarning(path + ": 知らない項目 \"" + kv.Key + "\" は無視します（綴りを確認してください）");
                    continue;
                }
                field.SetValue(result, ToType(kv.Value, field.FieldType, path + "." + kv.Key));
            }
            return result;
        }
        throw Fail(path, type.Name + " 型には対応していません");
    }

    private static Dictionary<string, FieldInfo> FieldsOf(Type type)
    {
        Dictionary<string, FieldInfo> map;
        if (fieldCache.TryGetValue(type, out map)) return map;
        map = new Dictionary<string, FieldInfo>();
        foreach (var f in type.GetFields(BindingFlags.Public | BindingFlags.Instance))
        {
            var key = f.GetCustomAttribute<JsonKeyAttribute>();
            map[(key != null ? key.Key : f.Name).ToLowerInvariant()] = f;
        }
        fieldCache[type] = map;
        return map;
    }

    private static object ToEnum(object node, Type type, string path)
    {
        var name = node as string;
        if (name == null) throw Fail(path, "文字列で書いてください（使える値: " + string.Join(", ", Enum.GetNames(type)) + "）");
        foreach (var candidate in Enum.GetNames(type))
        {
            if (string.Equals(candidate, name, StringComparison.OrdinalIgnoreCase)) return Enum.Parse(type, candidate);
        }
        throw Fail(path, "\"" + name + "\" は使えません（使える値: " + string.Join(", ", Enum.GetNames(type)) + "）");
    }

    private static double ToDouble(object node, string path)
    {
        if (node is double) return (double)node;
        throw Fail(path, "数値が必要です");
    }

    private static List<object> AsList(object node, string path)
    {
        var list = node as List<object>;
        if (list == null) throw Fail(path, "配列 [ ... ] が必要です");
        return list;
    }

    private static Dictionary<string, object> AsObject(object node, string path)
    {
        var dict = node as Dictionary<string, object>;
        if (dict == null) throw Fail(path, "オブジェクト { ... } が必要です");
        return dict;
    }

    private static FormatException Fail(string path, string message)
    {
        return new FormatException(path + ": " + message);
    }
}
