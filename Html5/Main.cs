using System;

namespace Html5
{
	class MainClass
	{
		public static void Main (string[] args)
		{
			var path = "/Users/fak/Desktop/HTML5.html";

			using (var reader = new System.IO.StreamReader (path)) {
				var b = new HDocumentBuilder (reader);
				var hdoc = b.Document;
				System.Console.WriteLine (hdoc);
			}
		}
	}
}
