using System;

namespace Html5
{
	/// <summary>
	/// http://dvcs.w3.org/hg/domcore/raw-file/tip/Overview.html#interface-document
	/// </summary>
	public class HDocument : HNode
	{
		public HDocument ()
		{
		}

		public override string OuterHtml {
			get {
				return (LastChild != null) ? LastChild.OuterHtml : "";
			}
		}
	}
}

