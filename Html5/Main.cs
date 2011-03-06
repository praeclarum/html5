using System;

namespace Html5
{
	class MainClass
	{
		public static void Main (string[] args)
		{
			var path = "/Users/fak/Desktop/HTML5.html";
			using (var reader = new System.IO.StreamReader (path)) {
				var tok = new Html5.Tokenizer.Tokenizer (reader);
				tok.Run ();
			}
		}
	}
}
