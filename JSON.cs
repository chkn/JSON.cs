using System;
using System.Linq;

public static class JSON 
{
	public static string Stringify (object obj)
	{
		string str;
		if (obj == null) return "null";
		if (obj is ValueType) return obj.ToString ().ToLowerInvariant ();
		if ((str = obj as string) != null) return System.Text.RegularExpressions.Regex.Escape (str);

		// assume it's a POCO
		return "{" + string.Join (",",
			from p in obj.GetType ().GetProperties ()
			let json = (JSONAttribute) p.GetCustomAttributes (typeof (JSONAttribute), true).FirstOrDefault ()
			where json != null
			select "\"" + (json.Key ?? p.Name) + "\":" + Stringify (p.GetValue (obj, null))
		) + "}";
	}
}

public class JSONAttribute : Attribute
{
	public string Key { get; set; }
	public JSONAttribute () {}	
	public JSONAttribute (string key) { Key = key; }
}