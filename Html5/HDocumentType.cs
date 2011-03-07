using System;

namespace Html5
{
	public class HDocumentType : HNode
	{
		public string Name { get; set; }
		public string PublicId { get; set; }
		public string SystemId { get; set; }

		public HDocumentType ()
		{
		}

		public override string OuterHtml {
			get {
				return "<!DOCTYPE " + Name + ">";
			}
		}
	}
}

