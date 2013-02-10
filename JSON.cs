using System;
using System.Linq;
using System.Reflection;
using System.Text;

public static class JSON {

	public static string Stringify (object obj)
	{
		if (obj == null)
			return "null";

		var str = obj as string;
		if (str != null)
			return Stringify (str);

		if (obj is ValueType)
			return Stringify ((ValueType)obj);

		// assume it's a POCO
		var buffer = new StringBuilder ();
		var needsComma = false;
		buffer.Append ('{');
		foreach (var prop in obj.GetType ().GetProperties (BindingFlags.Public | BindingFlags.Instance)) {
			if (prop.IsSpecialName)
				continue;

			var json = (JSONAttribute)prop.GetCustomAttributes (typeof (JSONAttribute), true).FirstOrDefault ();
			if (json == null)
				continue;

			if (needsComma)
				buffer.Append (',');
			buffer.Append ('"');
			buffer.Append (json.Key ?? prop.Name);
			buffer.Append ("\":");
			buffer.Append (JSON.Stringify (prop.GetValue (obj, null)));
			needsComma = true;
		}
		buffer.Append ('}');
		return buffer.ToString ();
	}

	public static string Stringify (string str)
	{
		if (str == null)
			return "null";

		// we init with a bit of extra space for quotes and possible escapes
		var buffer = new StringBuilder (str.Length + 10);
		buffer.Append ('"');
		for (var i = 0; i < str.Length; i++) {
			var next = str [i];
			switch (next) {

			case '\a' : buffer.Append ("\\a"); break;
			case '\b' : buffer.Append ("\\b"); break;
			case '\f' : buffer.Append ("\\f"); break;
			case '\n' : buffer.Append ("\\n"); break;
			case '\r' : buffer.Append ("\\r"); break;
			case '\t' : buffer.Append ("\\t"); break;
			case '\v' : buffer.Append ("\\v"); break;
			case '\\' : buffer.Append ("\\\\"); break;
			case '"' : buffer.Append ("\\\""); break;
			default : buffer.Append (next); break;
			}
		}
		buffer.Append ('"');
		return buffer.ToString ();
	}

	public static string Stringify (ValueType val)
	{
		//FIXME: handle enums better?
		return val.ToString ().ToLowerInvariant ();
	}
}

public class JSONAttribute : Attribute {

	public string Key { get; set; }

	public JSONAttribute ()
	{
	}
	public JSONAttribute (string key)
	{
		Key = key;
	}
}


