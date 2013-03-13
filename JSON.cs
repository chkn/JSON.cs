//
// JSON.cs
//
// Author:
//	Alex Corrado <alexander.corrado@gmail.com>
//
// Copyright (c) 2013 Alex Corrado
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.

using System;
using System.IO;
using System.Text;
using System.Linq;
using System.Reflection;
using System.Globalization;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;

public static class JSON {

	public static string Stringify (object obj, bool allProperties = true)
	{
		var buf = new StringBuilder ();
		Stringify (obj, buf, allProperties);
		return buf.ToString ();
	}
	public static void Stringify (object obj, StringBuilder buf, bool allProperties = true)
	{
		bool first;
		string str;
		IDictionary dict;
		IEnumerable list;
		     if (obj == null) buf.Append ("null");
		else if (true.Equals (obj)) buf.Append ("true");
		else if (false.Equals (obj)) buf.Append ("false");
		else if (obj is ValueType)
			buf.Append (string.Format (CultureInfo.InvariantCulture, "{0}", obj));
		else if ((str = obj as string) != null) {
			buf.Append ('"');
			buf.Append (Regex.Escape (str));
			buf.Append ('"');

		} else if ((dict = obj as IDictionary) != null) {
			buf.Append ('{');
			first = true;
			var item = dict.GetEnumerator ();
			while (item.MoveNext ()) {
				if (!first)
					buf.Append (',');
				buf.Append ('"');
				Stringify (item.Key, buf);
				buf.Append ("\":");
				Stringify (item.Value, buf);
				first = false;
			}
			buf.Append ('}');

		} else if ((list = obj as IEnumerable) != null) {
			buf.Append ('[');
			first = true;
			foreach (var item in list) {
				if (!first)
					buf.Append (',');
				Stringify (item, buf);
				first = false;
			}
			buf.Append (']');

		} else {
			// assume it's a POCO
			buf.Append ('{');
			first = true;
			foreach (var p in obj.GetType ().GetProperties ()) {
				var json = (JSONAttribute) p.GetCustomAttributes (typeof (JSONAttribute), true).FirstOrDefault ();
				if (json != null || allProperties) {
					if (!first)
						buf.Append (',');
					buf.Append ('"');
					buf.Append (json != null ? json.Key ?? p.Name : p.Name);
					buf.Append ("\":");
					Stringify (p.GetValue (obj, null), buf);
					first = false;
				}
			}
			buf.Append ('}');
		}
	}

	public static T Parse<T> (string str)
	{
		return (T) Parse (new StringReader (str), typeof (T));
	}
	public static T Parse<T> (TextReader reader)
	{
		return (T) Parse (reader, typeof (T));
	}
	public static object Parse (TextReader str, Type hint)
	{
		str.ConsumeWhitespace ();
		if (str.Peek () == 'n') return str.Expect ("null", null);
		if (str.Peek () == 't') return ConvertIfNeeded (str.Expect ("true", true), hint);
		if (str.Peek () == 'f') return ConvertIfNeeded (str.Expect ("false", false), hint);
		if (str.Peek () == '"')	return ConvertIfNeeded (str.ReadQuotedString (), hint);
		if (str.Peek () == '[') {
			str.Read (); // consume '['
			str.ConsumeWhitespace ();

			var next = str.Peek ();
			var items = new List<object> ();
			var elementType = GetElementType (hint);
			while (next != ']') {
				items.Add (Parse (str, elementType));
				str.ConsumeWhitespace ();
				next = str.Read ();
				if (next != ',' && next != ']')
					throw new JSONException (", or ]");
			}
			if (hint.IsArray) {
				var array = Array.CreateInstance (elementType, items.Count);
				if (elementType.IsClass)
					items.CopyTo ((object[])array);
				else
					for (var i = 0; i < items.Count; i++)
						array.SetValue (items [i], i);
				return array;
			}
			if (typeof (IList).IsAssignableFrom (hint) && hint != typeof (IList)) {
				var list = (IList) Activator.CreateInstance (hint);
				foreach (var item in items)
					list.Add (ConvertIfNeeded (item, elementType));
				return list;
			}
			return items;
		}
		if (str.Peek () == '{') {
			str.Read (); // consume '{'
			str.ConsumeWhitespace ();

			var next = str.Peek ();
			// FIXME: this fails if you pass in an interface like IDictionary<string,object>
			var obj = Activator.CreateInstance (hint);
			while (next != '}') {
				var key = str.ReadQuotedString ();
				str.ConsumeWhitespace ();
				if (str.Read () != ':')
					throw new JSONException (":");
				str.ConsumeWhitespace ();

				var parsed = false;
				var dict = obj as IDictionary;
				if (dict != null) {
					dict.Add (key, Parse (str, GetElementType (hint)));
					str.ConsumeWhitespace ();
					parsed = true;
				} else if (hint != typeof (object)) {
					//assume POCO
					var prop = (from p in hint.GetProperties ()
					            let json = (JSONAttribute) p.GetCustomAttributes (typeof (JSONAttribute), true).FirstOrDefault ()
					            where p.CanWrite && ((json != null && json.Key == key) || (json == null && p.Name == key))
					            select p).FirstOrDefault ();
					if (prop != null) {
						prop.SetValue (obj, Parse (str, prop.PropertyType), null);
						parsed = true;
					}
				}
				if (!parsed) {
					// just to consume..
					Parse (str, typeof (object));
					str.ConsumeWhitespace ();
				}
				next = str.Read ();
				if (next != ',' && next != '}')
					throw new JSONException (", or }");
				str.ConsumeWhitespace ();
			}
			return obj;
		}
		var number = str.ReadNumber ();
		if (number != "")
			return ConvertIfNeeded (number, hint);
		throw new JSONException ("valid JSON");
	}
	static object Expect (this TextReader reader, string str, object result)
	{
		for (var i = 0; i < str.Length; i++) {
			if (reader.Read () != str [i])
				throw new JSONException (str);
		}
		return result;
	}
	static string ReadQuotedString (this TextReader reader)
	{
		int current;
		var escaped = false;
		if (reader.Read () != '"')
			throw new JSONException ("\"");
		var buf = new StringBuilder ();
		while ((current = reader.Read ()) != '"' || escaped) {
			if (current == '\\' && !escaped) {
				escaped = true;
				continue;
			}
			if (escaped) {
				switch (current) {
				case 'a' : current = '\a'; break;
				case 'b' : current = '\b'; break;
				case 'f' : current = '\f'; break;
				case 'n' : current = '\n'; break;
				case 'r' : current = '\r'; break;
				case 't' : current = '\t'; break;
				case 'v' : current = '\v'; break;
				}
				escaped = false;
			}
			buf.Append ((char)current);
		}
		return buf.ToString ();
	}
	static string ReadNumber (this TextReader reader)
	{
		var buf = new StringBuilder ();
		var current = (char)reader.Peek ();
		while (current == '-' || current == '.' || current == 'e' || current == 'E' || char.IsDigit (current)) {
			reader.Read ();
			buf.Append (current);
			current = (char)reader.Peek ();
		}
		return buf.ToString ();
	}
	static void ConsumeWhitespace (this TextReader reader)
	{
		while (char.IsWhiteSpace ((char)reader.Peek ()))
			reader.Read ();
	}
	static Type GetElementType (Type type)
	{
		if (type.IsArray) return type.GetElementType ();
		if (type.IsGenericType) {
			type = type.GetGenericArguments ().Last ();
		    return type == typeof (object) ? typeof (Dictionary<string,object>) : type;
		}
		return typeof (object);
	}
	static object ConvertIfNeeded (object value, Type hint)
	{
		return hint != null && typeof (IConvertible).IsAssignableFrom (hint) ? Convert.ChangeType (value, hint) : value;
	}
}

public class JSONAttribute : System.Attribute {
	public string Key { get; set; }
	public JSONAttribute () {}	
	public JSONAttribute (string key) { Key = key; }
}
public class JSONException : System.Exception {
	public JSONException (string expected)
		: base (string.Format ("expecting {0}", expected)) {}
}