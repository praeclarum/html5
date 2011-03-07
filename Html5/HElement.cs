using System;
using System.Text;

namespace Html5
{
	public class HElement : HNode
	{
		public HElement ()
		{
		}

		public HElement (string name) : base (name)
		{
		}

		public override string OuterHtml {
			get {
				var sb = new StringBuilder ();

				sb.Append ("<");
				sb.Append (NodeName);
				sb.Append (">");

				foreach (var ch in ChildNodes) {
					sb.Append (ch.OuterHtml);
				}

				sb.Append ("</");
				sb.Append (NodeName);
				sb.Append (">");

				return sb.ToString ();
			}
		}

		public override string ToString ()
		{
			return NodeName;
		}
	}
}
