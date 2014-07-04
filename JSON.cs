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
//
// Preprocessor defines:
//    ENABLE_DYNAMIC - enable a dynamic overload of JSON.Parse
//    NET_45         - define this for Windows Store or PCL Profiles
//                        without classic reflection...
#if !NET_45
using TypeInfo = System.Type;
#endif

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

	const string DATETIME_FORMAT = "yyyy-MM-dd'T'HH:mm:ss.fffK";

	#region Stringify

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
		else if (obj is DateTime) {
			buf.Append ('"');
			buf.Append (((DateTime)obj).ToUniversalTime ().ToString (DATETIME_FORMAT));
			buf.Append ('"');
		} else if ((str = obj as string) != null) {
			buf.Append ('"');
			foreach (var current in str) {
				switch (current) {
				case '\a' : buf.Append ("\\a"); break;
				case '\b' : buf.Append ("\\b"); break;
				case '\f' : buf.Append ("\\f"); break;
				case '\n' : buf.Append ("\\n"); break;
				case '\r' : buf.Append ("\\r"); break;
				case '\t' : buf.Append ("\\t"); break;
				case '\v' : buf.Append ("\\v"); break;
				case '\\' : buf.Append ("\\\\"); break;
				case '"'  : buf.Append ("\\\""); break;
				default   : buf.Append (current); break;
				}
			}
			buf.Append ('"');
		} else if (obj is IFormattable) {
			buf.Append (string.Format (CultureInfo.InvariantCulture, "{0}", obj));
		} else if ((dict = obj as IDictionary) != null) {
			buf.Append ('{');
			first = true;
			var item = dict.GetEnumerator ();
			while (item.MoveNext ()) {
				if (!first)
					buf.Append (',');
				Stringify (item.Key.ToString (), buf, allProperties);
				buf.Append (':');
				Stringify (item.Value, buf, allProperties);
				first = false;
			}
			buf.Append ('}');
		} else if ((list = obj as IEnumerable) != null) {
			buf.Append ('[');
			first = true;
			foreach (var item in list) {
				if (!first)
					buf.Append (',');
				Stringify (item, buf, allProperties);
				first = false;
			}
			buf.Append (']');
		} else {
			// assume it's a POCO
			buf.Append ('{');
			first = true;
			foreach (var p in obj.GetType ().GetTypeInfo ().GetProperties ()) {
				var json = p.GetCustomAttribute<JSONAttribute> (true);
				if (json != null || allProperties) {
					if (!first)
						buf.Append (',');
					buf.Append ('"');
					buf.Append (json != null ? json.Key ?? p.Name : p.Name);
					buf.Append ("\":");
					Stringify (ConvertIfNeeded (p.GetValue (obj, null), json != null ? json.Type : null), buf, allProperties);
					first = false;
				}
			}
			buf.Append ('}');
		}
	}

	#endregion

	#region Parse

	#if ENABLE_DYNAMIC
	public static dynamic Parse (string str)
	{
		return Parse (new StringReader (str));
	}
	public static dynamic Parse (TextReader reader)
	{
		return Parse (reader, typeof (object));
	}
	#endif
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
		switch (str.Peek ()) {
		case 'n': return str.Expect ("null", null);
		case 't': return ConvertIfNeeded (str.Expect ("true", true), hint);
		case 'f': return ConvertIfNeeded (str.Expect ("false", false), hint);
		case '"': return ConvertIfNeeded (str.ReadQuotedString (), hint);
		case '[': {
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
				for (var i = 0; i < items.Count; i++)
					array.SetValue (items [i], i);
				return array;
			}
			if (hint != typeof (IList) && typeof (IList).GetTypeInfo ().IsAssignableFrom (hint.GetTypeInfo ())) {
				var list = (IList) Activator.CreateInstance (hint);
				foreach (var item in items)
					list.Add (ConvertIfNeeded (item, elementType));
				return list;
			}
			return items;
		}
		case '{': {
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
					dict.Add (ConvertIfNeeded (key, GetKeyType (hint)), Parse (str, GetElementType (hint)));
					parsed = true;
				} else if (hint != typeof (object)) {
					//assume POCO
					foreach (var p in hint.GetTypeInfo ().GetProperties ()) {
						var json = p.GetCustomAttribute<JSONAttribute> (true);
						if (p.CanWrite && key == (json != null ? json.Key ?? p.Name : p.Name)) {
							p.SetValue (obj, Parse (str, p.PropertyType), null);
							parsed = true;
							break;
						}
					}
				}
				if (!parsed) {
					// just to consume..
					Parse (str, typeof (object));
				}
				str.ConsumeWhitespace ();
				next = str.Read ();
				if (next != ',' && next != '}')
					throw new JSONException (", or }");
				str.ConsumeWhitespace ();
			}
			return obj;
		} } // end switch
		var number = str.ReadNumber ();
		if (number != "")
			return ConvertIfNeeded (number, hint);
		throw new JSONException ("valid JSON");
	}

	#endregion

	#region Scanner
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
	#endregion

	#region Helpers
	static Type GetKeyType (Type type)
	{
		return type.IsGeneric () ? type.GetGenericArguments ().First () : typeof (object);
	}
	static Type GetElementType (Type type)
	{
		if (type.IsArray) return type.GetElementType ();
		if (type.IsGeneric ()) {
			type = type.GetGenericArguments ().Last ();
		    return type == typeof (object) ? typeof (Dictionary<string,object>) : type;
		}
		return typeof (object);
	}
	static object ConvertIfNeeded (object value, Type hint)
	{
		string str;
		if (hint == typeof (DateTime) && (str = value as string) != null) {
			return DateTime.ParseExact (str, DATETIME_FORMAT, CultureInfo.InvariantCulture);
		} else {
			try {
				return hint != null ? Convert.ChangeType (value, hint) : value;
			} catch (InvalidCastException) {
				return value;
			}
		}
	}
	#endregion

	#region Compatibility shims
	static bool IsGeneric (this Type type)
	{
		#if NET_45
		return type.IsConstructedGenericType;
		#else
		return type.IsGenericType;
		#endif
	}

	#if NET_45
	static IEnumerable<PropertyInfo> GetProperties (this TypeInfo typeInfo)
	{
		return typeInfo.DeclaredProperties;
	}
	static IEnumerable<Type> GetGenericArguments (this Type typeInfo)
	{
		return typeInfo.GenericTypeArguments;
	}
	#else
	static TypeInfo GetTypeInfo (this Type type)
	{
		return type;
	}
	#endif
	#endregion
}

public class JSONAttribute : System.Attribute {
	public string Key { get; set; }
	public Type Type { get; set; }
	public JSONAttribute () {}
	public JSONAttribute (string key) { Key = key; }
}
public class JSONException : System.Exception {
	public JSONException (string expected)
		: base (string.Format ("expecting {0}", expected)) {}
}