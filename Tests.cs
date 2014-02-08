using System;
using System.Reflection;
using System.Collections.Generic;

static class Tests {

	// To run (on mono):
	//     mcs -debug Tests.cs JSON.cs && mono --debug Tests.exe
	//
	// To add a new test, just add a new non-public static method

	static void StringifyStrings ()
	{
		AssertStringify ("\"foo\"", "foo");
		AssertStringify ("\"foo\\bbar\"", "foo\bbar", "\\b");
		AssertStringify ("\"foo\\fbar\"", "foo\fbar", "\\f");
		AssertStringify ("\"foo\\nbar\"", "foo\nbar", "\\n");
		AssertStringify ("\"foo\\rbar\"", "foo\rbar", "\\r");
		AssertStringify ("\"foo\\tbar\"", "foo\tbar", "\\t");
		AssertStringify ("\"foo\\\\bar\"", "foo\\bar", "\\");
	}

	static void ParseStrings ()
	{
		AssertParse ("foo\bbar", "\"foo\\bbar\"", "\\b");
		AssertParse ("foo\fbar", "\"foo\\fbar\"", "\\f");
		AssertParse ("foo\nbar", "\"foo\\nbar\"", "\\n");
		AssertParse ("foo\rbar", "\"foo\\rbar\"", "\\r");
		AssertParse ("foo\tbar", "\"foo\\tbar\"", "\\t");
		AssertParse ("foo\\bar", "\"foo\\\\bar\"", "\\");
		AssertParse ("foo\\x00FFbar", "\"foo\\u00FFbar\"", "Unicode escape seq");
	}

	static void StringifyValueTypes ()
	{
		AssertStringify ("true", true);
		AssertStringify ("false", false);
		AssertStringify ("10", 10);
		AssertStringify ("55.7", 55.7f);
		AssertStringify ("-5", (-5));
	}

	static void ParseValueTypes ()
	{
		AssertParse<bool>  (true, "true", "true");
		AssertParse<bool>  (false, "false", "false");
		AssertParse<int>   (10, "10", "10");
		AssertParse<float> (55.7f, "55.7", "55.7f");
		AssertParse<double>(48.2d, "48.2", "48.2d");
		AssertParse<int>   (-5, "-5", "-5");
		AssertParse<long>  ((long)(13e7), "13e7", "13e7");
	}

	static void StringifyObjects ()
	{
		AssertStringify ("{\"Foo\":\"bar\",\"Baz\":false,\"Xam\":{\"Nested\":10.5}}",
			new {  Foo  = "bar",   Baz = false,  Xam=new{ Nested = 10.5f } });

		AssertStringify ("{\"Ten\":10,\"Hoopla\":true,\"Monkey\":\"Awesome\"}",
			new Dictionary<string,object> { { "Ten", 10 }, { "Hoopla", true }, { "Monkey", "Awesome" } });

		AssertStringify ("[1,2,4,3]", new[] { 1, 2, 4, 3 });
		AssertStringify ("[\"hello\",10,{\"hi\":5}]", new List<object> { "hello", 10, new { hi = 5 } });
	}

	static void ParseObjects ()
	{
		//FIXME: more
		AssertParse (
			new Dictionary<string,object> { { "Ten", 10 }, { "Hoopla", true }, { "Monkey", "Awesome" } },
			"{\"Ten\":10,\"Hoopla\":true,\"Monkey\":\"Awesome\"}",
			"Dictionary<string,object>",
			new DictionaryEqualityComparer<string,object> ()
		);
	}


	static void ParseNonStringKeys ()
	{
		AssertParse<Dictionary<int,int>> (new Dictionary<int,int> { { 1, 2 }, {4, 3} }, "{\"1\":2,\"4\":3}", "#1", new DictionaryEqualityComparer<int,int> ());
	}
	#region Harness
	static void AssertStringify (string expected, object value, string message = null)
	{
		var actual = JSON.Stringify (value);
		if (actual != expected)
			throw new fail (expected, actual, message);
	}
	static void AssertParse<TVal> (TVal expected, string json, string message = null, IEqualityComparer<TVal> comparer = null)
	{
		if (comparer == null)
			comparer = EqualityComparer<TVal>.Default;
		var actual = JSON.Parse<TVal> (json);
		if (!comparer.Equals (actual, expected))
			throw new fail (expected, actual, message);
	}
	static void Main ()
	{
		foreach (var method in typeof (Tests).GetMethods (BindingFlags.NonPublic | BindingFlags.Static)) {
			if (method.Name == "Main" || method.Name.StartsWith ("Assert"))
				continue;
			try {
				Console.Write ("Running '{0}' ... ", method.Name);
				method.Invoke (null, null);
			} catch (Exception ex) {
				if (ex is TargetInvocationException)
					ex = ex.InnerException;
				Console.WriteLine (ex);
				continue;
			}
			Console.WriteLine ("pass");
		}
	}
	#endregion
}

class DictionaryEqualityComparer<TKey,TVal> : IEqualityComparer<Dictionary<TKey,TVal>> {
	IEqualityComparer<TVal> cval;
	public DictionaryEqualityComparer (IEqualityComparer<TVal> cval = null)
	{
		this.cval = cval ?? EqualityComparer<TVal>.Default;
	}
	public bool Equals (Dictionary<TKey, TVal> x, Dictionary<TKey, TVal> y)
	{
		foreach (var kvx in x) {
			TVal yval;
			if (!y.TryGetValue (kvx.Key, out yval) || !cval.Equals (kvx.Value, yval))
				return false;
		}
		foreach (var kvy in y) {
			TVal xval;
			if (!x.TryGetValue (kvy.Key, out xval) || !cval.Equals (kvy.Value, xval))
				return false;
		}
		return true;
	}

	public int GetHashCode (Dictionary<TKey, TVal> obj)
	{
		return obj.GetHashCode ();
	}
}

class fail : Exception {
	public fail (object expected, object actual, string message = null)
		: base (string.Format ("{0}Expected: {1}{2}Actual: {3}", message + Environment.NewLine, expected, Environment.NewLine, actual))
	{
	}
}

